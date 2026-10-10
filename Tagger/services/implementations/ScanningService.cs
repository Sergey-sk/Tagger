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

        public async Task ScanDirectoryAsync(string path, string lastSuccessScanTime, IProgress<int> progress, CancellationToken cancellationToken)
        {
            var filesChannel = Channel.CreateUnbounded<(string filePaths, DateTime lastWriteTime, long fileSize)>();

            var dbWorkerTask = WriteToDbAsync(path, lastSuccessScanTime, filesChannel, progress, cancellationToken);
            var scanTask = ScanFilesAsync(path, lastSuccessScanTime, filesChannel, cancellationToken);

            try
            {
                await Task.WhenAll(scanTask, dbWorkerTask);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task ScanFilesAsync(string path,
                                          string lastSuccessScanTime,
                                          Channel<(string filePath, DateTime lastWriteTime, long fileSize)> filesChannel,
                                          CancellationToken cancellationToken)
        {
            try
            {
                var lastTime = string.IsNullOrEmpty(lastSuccessScanTime) ? DateTime.MinValue : DateTime.Parse(lastSuccessScanTime);
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
                            await filesChannel.Writer.WriteAsync((file.FullName, file.LastWriteTime, file.Length), token);
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
            }
        }

        public async Task WriteToDbAsync(string rootPath,
                                         string lastTime,
                                         Channel<(string filePath, DateTime lastWriteTime, long fileSize)> filesChannel,
                                         IProgress<int> progress,
                                         CancellationToken cancellationToken)
        {
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await context.Set<TempScannedFile>().ExecuteDeleteAsync(cancellationToken);

            int totalProcessed = 0;
            List<(string filePath, DateTime lastWriteTime, long fileSize)> batch = new List<(string filePath, DateTime lastWriteTime, long fileSize)>(5000);
            var lastUiUpdateTime = DateTime.Now;
            var lastScanTime = string.IsNullOrEmpty(lastTime) ? DateTime.MinValue : DateTime.Parse(lastTime);

            try
            {
                await foreach (var filePath in filesChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    batch.Add(filePath);

                    if (batch.Count == 5000)
                    {
                        totalProcessed += batch.Count;
                        await InsertDataIntoTemporaryTable(batch, context, lastScanTime, cancellationToken);

                        if ((DateTime.Now - lastUiUpdateTime).TotalMilliseconds >= 500)
                        {
                            progress.Report(totalProcessed);
                            lastUiUpdateTime = DateTime.Now;
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await InsertDataIntoTemporaryTable(batch, context, lastScanTime, cancellationToken);
                    totalProcessed += batch.Count;
                }

                progress.Report(totalProcessed);

                await context.Files
                    .Where(f => f.Path.StartsWith(rootPath) && !context.Set<TempScannedFile>().Any(t => t.Path == f.Path))
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, true), cancellationToken);

                await context.Files
                    .Where(f => f.Path.StartsWith(rootPath) && f.IsDeleted && context.Set<TempScannedFile>().Any(t => t.Path == f.Path))
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, false), cancellationToken);

                progress.Report(totalProcessed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task InsertDataIntoTemporaryTable(List<(string filePath, DateTime lastWriteTime, long fileSize)> batch,
                                                        ApplicationDbContext context,
                                                        DateTime lastTime,
                                                        CancellationToken token)
        {
            var fileRecords = new List<FileRecord>(batch.Count);
            var tempRecords = new List<TempScannedFile>(batch.Count);

            foreach (var file in batch)
            {
                tempRecords.Add(new TempScannedFile { Path = file.filePath });

                fileRecords.Add(new FileRecord
                {
                    Name = Path.GetFileName(file.filePath),
                    Path = file.filePath,
                    Size = file.fileSize,
                    LastModified = file.lastWriteTime,
                    IsDeleted = false
                });
            }

            await context.BulkInsertAsync(tempRecords, cancellationToken: token);

            if (fileRecords.Count > 0)
            {
                var bulkConfig = new BulkConfig
                {
                    UpdateByProperties = new List<string> { nameof(FileRecord.Path) }
                };
                await context.BulkInsertOrUpdateAsync(fileRecords, bulkConfig, cancellationToken: token);
            }

            context.ChangeTracker.Clear();
            batch.Clear();
        }
    }
}
