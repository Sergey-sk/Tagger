using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Channels;
using Tagger.db;
using Tagger.model;
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

        public async Task ScanDirectoryAsync(string path, string lastSuccessScanTime, IProgress<List<FileRecord>> progress, CancellationToken cancellationToken)
        {
            var filesChannel = Channel.CreateUnbounded<string>();

            var dbWorkerTask = WriteToDbAsync(path, filesChannel, progress, cancellationToken);
            var scanTask = ScanFilesAsync(path, lastSuccessScanTime, filesChannel, cancellationToken);

            await Task.WhenAll(scanTask, dbWorkerTask);
        }

        private async Task ScanFilesAsync(string path, string lastSuccessScanTime, Channel<string> filesChannel, CancellationToken cancellationToken)
        {
            try
            {
                //var lastTime = string.IsNullOrEmpty(lastSuccessScanTime) ? DateTime.MinValue : DateTime.Parse(lastSuccessScanTime);
                var lastTime = DateTime.MinValue;
                List<DirectoryInfo> tasks = [];
                DirectoryInfo rootDir = new DirectoryInfo(path);

                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                };

                var subDirectories = rootDir.EnumerateDirectories("*", options);

                tasks.Add(rootDir);
                tasks.AddRange(subDirectories);

                await Parallel.ForEachAsync(tasks,
                    new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                    async (subDir, token) =>
                {
                    try
                    {
                        var enumerationOptions = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = subDir.FullName != rootDir.FullName,
                            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                        };
                        foreach (var file in subDir.EnumerateFiles("*", enumerationOptions))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (file.LastWriteTime < lastTime)
                                continue;
                            await filesChannel.Writer.WriteAsync(file.FullName, token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                });

                filesChannel.Writer.TryComplete();
            }
            catch (Exception)
            {
                filesChannel.Writer.TryComplete();
                throw;
            }
        }

        public async Task WriteToDbAsync(string rootPath, Channel<string> filesChannel, IProgress<List<FileRecord>> progress, CancellationToken cancellationToken)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await context.Set<TempScannedFile>().ExecuteDeleteAsync();

            int totalProcessed = 0;
            List<string> batch = new List<string>(5000);
            var lastUiUpdateTime = DateTime.Now;

            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await foreach (var filePath in filesChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    batch.Add(filePath);

                    if (batch.Count == 5000)
                    {
                        await InsertDataIntoTemporaryTable(batch, context);
                        totalProcessed += batch.Count;

                        if ((DateTime.Now - lastUiUpdateTime).TotalMilliseconds >= 500)
                        {
                            ReportProgressToUi(progress, totalProcessed);
                            lastUiUpdateTime = DateTime.Now;
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await InsertDataIntoTemporaryTable(batch, context);
                    totalProcessed += batch.Count;
                }

                await transaction.CommitAsync();
                ReportProgressToUi(progress, totalProcessed);

                await context.Files
                    .Where(f => f.Path.StartsWith(rootPath) && !context.Set<TempScannedFile>().Any(t => t.Path == f.Path))
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, true));

                progress.Report([]);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task InsertDataIntoTemporaryTable(List<string> batch, ApplicationDbContext context)
        {
            var fileRecords = new List<FileRecord>(batch.Count);
            var tempRecords = new List<TempScannedFile>(batch.Count);

            foreach (var path in batch)
            {
                fileRecords.Add(new FileRecord
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Size = 0,
                    LastModified = DateTime.Now,
                    IsDeleted = false
                });

                tempRecords.Add(new TempScannedFile { Path = path });
            }

            await context.BulkInsertAsync(fileRecords);
            await context.BulkInsertAsync(tempRecords);

            context.ChangeTracker.Clear();
            batch.Clear();
        }

        private void ReportProgressToUi(IProgress<List<FileRecord>> progress, int totalProcessed)
        {
            if (progress == null) return;

            var progressInfo = new FileRecord
            {
                Name = $"Сканирование... Найдено: {totalProcessed}",
                Size = totalProcessed
            };

            progress.Report([progressInfo]);
        }

        //public async Task ScanDirectoryAsync(string path, IProgress<List<FileRecord>> progress, CancellationToken cancellationToken)
        //{
        //    await Task.Run(async () =>
        //    {
        //        using var context = await _contextFactory.CreateDbContextAsync();
        //        context.ChangeTracker.AutoDetectChangesEnabled = false;

        //        try
        //        {
        //            int totalFilesCount = 0;
        //            const int DbBatchSize = 2000;
        //            const int UiBatchSize = 2000;

        //            var dbBatchList = new List<FileRecord>();
        //            var uiBatchList = new List<FileRecord>();
        //            var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        //            var existingPath = new HashSet<string>(
        //                await context.Files
        //                    .Where(f => f.Path.StartsWith(path))
        //                    .Select(f => f.Path)
        //                    .ToListAsync(cancellationToken),
        //                StringComparer.OrdinalIgnoreCase);

        //            var rootDir = new DirectoryInfo(path);
        //            var enumerationOptions = new EnumerationOptions
        //            {
        //                IgnoreInaccessible = true,
        //                RecurseSubdirectories = true,
        //                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        //            };

        //            foreach (var file in rootDir.EnumerateFiles("*", enumerationOptions))
        //            {
        //                cancellationToken.ThrowIfCancellationRequested();
        //                scannedPaths.Add(file.FullName);

        //                if (existingPath.Contains(file.FullName)) continue;

        //                var record = new FileRecord
        //                {
        //                    Name = file.Name,
        //                    Path = file.FullName,
        //                    Size = file.Length,
        //                    LastModified = file.LastWriteTime,
        //                    IsDeleted = false
        //                };

        //                dbBatchList.Add(record);
        //                uiBatchList.Add(record);
        //                totalFilesCount++;

        //                if (uiBatchList.Count >= UiBatchSize)
        //                {
        //                    progress.Report([.. uiBatchList]);
        //                    uiBatchList.Clear();
        //                }

        //                if (dbBatchList.Count >= DbBatchSize)
        //                {
        //                    await context.Files.AddRangeAsync(dbBatchList, cancellationToken);
        //                    await context.SaveChangesAsync(cancellationToken);

        //                    foreach (var added in dbBatchList) existingPath.Add(added.Path);
        //                    dbBatchList.Clear();
        //                }
        //            }

        //            if (uiBatchList.Count > 0)
        //            {
        //                progress.Report([.. uiBatchList]);
        //            }

        //            if (dbBatchList.Count > 0)
        //            {
        //                await context.Files.AddRangeAsync(dbBatchList, cancellationToken);
        //                await context.SaveChangesAsync(cancellationToken);
        //            }

        //            var dbFiles = await context.Files
        //                .Where(f => f.Path.StartsWith(path))
        //                .ToListAsync(cancellationToken);

        //            var filesToRemove = dbFiles
        //                .Where(f => !scannedPaths.Contains(f.Path))
        //                .ToList();

        //            if (filesToRemove.Any())
        //            {
        //                context.RemoveRange(filesToRemove);
        //                await context.SaveChangesAsync(cancellationToken);
        //            }

        //            progress.Report([]);
        //        }
        //        finally
        //        {
        //            context.ChangeTracker.AutoDetectChangesEnabled = true;
        //        }
        //    }, cancellationToken);
        //}
    }
}
