using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Buffers;
using System.Data;
using System.IO;
using System.IO.Enumeration;
using System.Threading.Channels;
using Tagger.db;
using Tagger.messages;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class ScanningService : IScanningService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ScanningService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task ScanDirectoryAsync(string path,
            string lastSuccessScanTime,
            IProgress<int>? progress,
            CancellationToken cancellationToken,
            bool isBackground = false)
        {
            var filesChannel = Channel.CreateUnbounded<ScannedFile>();
            long currentScanId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var dbWorkerTask = WriteToDbAsync(path, filesChannel, currentScanId, progress, cancellationToken, isBackground);
            var scanTask = ScanFilesAsync(path, filesChannel, cancellationToken);
            await Task.WhenAll(scanTask, dbWorkerTask);
        }

        private async Task ScanFilesAsync(string path,
                                          Channel<ScannedFile> fileChannel,
                                          CancellationToken cancellationToken)
        {
            var dirChannel = Channel.CreateUnbounded<string>();
            dirChannel.Writer.TryWrite(path);

            int pendingDirs = 1;

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
            };

            var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                await foreach (var currentDir in dirChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        var enumerable = new FileSystemEnumerable<int>(
                            currentDir,
                            (ref FileSystemEntry entry) =>
                            {
                                if (entry.IsDirectory)
                                {
                                    Interlocked.Increment(ref pendingDirs);
                                    dirChannel.Writer.TryWrite(entry.ToFullPath());
                                }
                                else
                                {
                                    fileChannel.Writer.TryWrite(new ScannedFile(
                                        entry.ToFullPath(),
                                        entry.LastWriteTimeUtc.LocalDateTime,
                                        entry.Length));
                                }

                                return 0;
                            }, options);

                        foreach (var _ in enumerable)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                        }
                    }
                    catch (Exception) { }
                    finally
                    {
                        if (Interlocked.Decrement(ref pendingDirs) == 0)
                        {
                            dirChannel.Writer.TryComplete();
                        }
                    }
                }
            }, cancellationToken));

            await Task.WhenAll(workers);
            fileChannel.Writer.TryComplete();
        }

        public async Task WriteToDbAsync(string rootPath,
                                         Channel<ScannedFile> filesChannel,
                                         long currentScanId,
                                         IProgress<int>? progress,
                                         CancellationToken cancellationToken,
                                         bool isBackground)
        {
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await context.Database.ExecuteSqlRawAsync(@"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA cache_size = -64000;
                PRAGMA temp_store = MEMORY;", cancellationToken);


            const int BatchSize = 50000;
            int totalProcessed = 0;
            ScannedFile[] batch = ArrayPool<ScannedFile>.Shared.Rent(BatchSize);
            var lastUiUpdateTime = DateTime.Now;
            int counter = 0;

            try
            {
                await foreach (var filePath in filesChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    batch[counter++] = filePath;

                    if (counter == BatchSize)
                    {
                        totalProcessed += counter;
                        await InsertFilesToDb(batch.AsMemory(0, counter), currentScanId, isBackground, context, cancellationToken);

                        Array.Clear(batch, 0, counter);
                        counter = 0;

                        if ((DateTime.Now - lastUiUpdateTime).TotalMilliseconds >= 500)
                        {
                            progress?.Report(totalProcessed);
                            lastUiUpdateTime = DateTime.Now;
                        }
                    }
                }

                if (counter > 0)
                {
                    await InsertFilesToDb(batch.AsMemory(0, counter), currentScanId, isBackground, context, cancellationToken);
                    totalProcessed += counter;
                    Array.Clear(batch, 0, counter);
                    counter = 0;
                }

                progress?.Report(totalProcessed);

                if (isBackground)
                {
                    var deletedFilesCount = await context.Files
                        .Where(f => f.Path.StartsWith(rootPath)
                                    && !f.IsDeleted
                                    && f.SyncStatus == 0
                                    && f.LastScanId != currentScanId)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.SyncStatus, 2), cancellationToken);

                    var newOrModifiedCount = await context.Files
                        .CountAsync(f => f.Path.StartsWith(rootPath) && (f.SyncStatus == 1 || f.PendingSize != null), cancellationToken);

                    if (newOrModifiedCount > 0 || deletedFilesCount > 0)
                    {
                        WeakReferenceMessenger.Default.Send(new BackgroundScanFoundChangesMessage(newOrModifiedCount, deletedFilesCount));
                    }
                }
                else
                {

                    await context.Files
                        .Where(f => f.Path.StartsWith(rootPath) && !f.IsDeleted && f.LastScanId != currentScanId)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, true), cancellationToken);
                }

                progress?.Report(totalProcessed);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                ArrayPool<ScannedFile>.Shared.Return(batch, clearArray: true);
            }
        }

        private async Task InsertFilesToDb(ReadOnlyMemory<ScannedFile> batch,
                                           long currentScanId,
                                           bool isBackground,
                                           ApplicationDbContext context,
                                           CancellationToken token)
        {
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(token);

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            if (!isBackground)
            {
                command.CommandText = @"
                    insert into Files(Name, Path, Size, LastModified, IsDeleted, LastScanId, SyncStatus, PendingSize, PendingLastModified)
                    values($name, $path, $size, $mod, 0, $scanId, 0, null, null)
                    on conflict(Path) do update set
                        Name = excluded.Name,
                        Size = excluded.Size,
                        LastModified = excluded.LastModified,
                        IsDeleted = 0,
                        LastScanId = excluded.LastScanId,
                        SyncStatus = 0,
                        PendingSize = null,
                        PendingLastModified = null;";
            }
            else
            {
                command.CommandText = @"
                        insert into Files(Name, Path, Size, LastModified, IsDeleted, LastScanId, SyncStatus, PendingSize, PendingLastModified)
                        values($name, $path, $size, $mod, 0, $scanId, 1, NULL, NULL)
                        on conflict(Path) do update set
                        LastScanId = excluded.LastScanId,
                        PendingSize = case
                            when Files.Size != excluded.Size or Files.LastModified != excluded.LastModified
                            then excluded.Size
                            else Files.PendingSize
                        end,
                        PendingLastModified = case
                            when Files.Size != excluded.Size OR Files.LastModified != excluded.LastModified
                            then excluded.LastModified
                            else Files.PendingLastModified
                        end;";
            }

            var pName = command.Parameters.Add("$name", SqliteType.Text);
            var pPath = command.Parameters.Add("$path", SqliteType.Text);
            var pSize = command.Parameters.Add("$size", SqliteType.Integer);
            var pMod = command.Parameters.Add("$mod", SqliteType.Text);
            var pScanId = command.Parameters.Add("$scanId", SqliteType.Integer);

            command.Prepare();

            foreach (ref readonly var file in batch.Span)
            {
                pName.Value = Path.GetFileName(file.Path.AsSpan()).ToString();
                pPath.Value = file.Path;
                pSize.Value = file.FileSize;
                pMod.Value = file.LastWriteTime;
                pScanId.Value = currentScanId;
                command.ExecuteNonQuery();
            }

            await transaction.CommitAsync(token);
        }

        public async Task ApplyBackgroundScanChangesAsync(string rootPath, CancellationToken cancellationToken)
        {
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var normalizedRoot = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            var upperBound = normalizedRoot + "\uffff";

            using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    update Files
                    set Size = coalesce(PendingSize, Size),
                        LastModified = coalesce(PendingLastModified, LastModified),
                        PendingSize = null,
                        PendingLastModified = null,
                        SyncStatus = 0,
                        IsDeleted = 0
                    where Path >= {0} and Path < {1}
                        and (SyncStatus = 1 or PendingSize is not null)",
                [normalizedRoot, upperBound], cancellationToken);

                await context.Database.ExecuteSqlRawAsync(@"
                    update Files
                    set IsDeleted = 1,
                        PendingSize = null,
                        PendingLastModified = null
                    where Path >= {0} and Path < {1}
                        and SyncStatus = 2",
                [normalizedRoot, upperBound], cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task DiscardBackgroundScanChangesAsync(string rootPath, CancellationToken cancellationToken)
        {
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var normalizedRoot = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            var upperBound = normalizedRoot + "\uffff";

            using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    delete from Files
                    where Path >= {0} and Path < {1}
                      and SyncStatus = 1",
                [normalizedRoot, upperBound], cancellationToken);

                await context.Database.ExecuteSqlRawAsync(@"
                    update Files
                    set SyncStatus = 0,
                        PendingSize = null,
                        PendingLastModified = null
                    where Path >= {0} and Path < {1}
                        and (SyncStatus = 2 or PendingSize is not null or PendingLastModified is not null)",
                [normalizedRoot, upperBound], cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    public readonly record struct ScannedFile(string Path, DateTime LastWriteTime, long FileSize);
}
