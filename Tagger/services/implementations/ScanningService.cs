using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

            var dbWorkerTask = WriteToDbAsync(path, lastSuccessScanTime, filesChannel, currentScanId, progress, cancellationToken, isBackground);
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

            var workers = Enumerable.Range(0, Environment.ProcessorCount).Select(_ => Task.Run(async () =>
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
                                        entry.FileName.ToString(),
                                        entry.ToFullPath(),
                                        entry.LastWriteTimeUtc.LocalDateTime,
                                        entry.Length));
                                }

                                return 0;
                            }, options);

                        foreach (var _ in enumerable)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
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
                                         string lastTime,
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


            int totalProcessed = 0;
            List<ScannedFile> batch = new(50000);
            var lastUiUpdateTime = DateTime.Now;

            try
            {
                await foreach (var filePath in filesChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    batch.Add(filePath);

                    if (batch.Count == 50000)
                    {
                        totalProcessed += batch.Count;
                        await InsertDataIntoTemporaryTable(rootPath, batch, currentScanId, isBackground, context, cancellationToken);

                        if ((DateTime.Now - lastUiUpdateTime).TotalMilliseconds >= 500)
                        {
                            progress?.Report(totalProcessed);
                            lastUiUpdateTime = DateTime.Now;
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await InsertDataIntoTemporaryTable(rootPath, batch, currentScanId, isBackground, context, cancellationToken);
                    totalProcessed += batch.Count;
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
                        .CountAsync(f => f.Path.StartsWith(rootPath) && (f.SyncStatus == 1 || f.PendingSize != null));

                    if (newOrModifiedCount > 0 || deletedFilesCount > 0)
                    {
                        WeakReferenceMessenger.Default.Send(new BackgroundScanFoundChangesMessage(newOrModifiedCount, deletedFilesCount));
                    }
                }
                else
                {

                    await context.Files
                        .Where(f => f.Path.StartsWith(rootPath) && !f.IsDeleted && f.LastScanId != currentScanId)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, true));
                }

                progress?.Report(totalProcessed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        private async Task InsertDataIntoTemporaryTable(string rootPath,
                                                        List<ScannedFile> batch,
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

            foreach (var file in batch)
            {
                pName.Value = file.Name;
                pPath.Value = file.Path;
                pSize.Value = file.FileSize;
                pMod.Value = file.LastWriteTime;
                pScanId.Value = currentScanId;
                command.ExecuteNonQuery();
            }

            await transaction.CommitAsync(token);
            batch.Clear();
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
                    SyncStatus = 0,
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

    }

    public readonly record struct ScannedFile(string Name, string Path, DateTime LastWriteTime, long FileSize);
}
