using Microsoft.EntityFrameworkCore;
using System.IO;
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

        public async Task ScanDirectoryAsync(string path, IProgress<List<FileRecord>> progress, CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.ChangeTracker.AutoDetectChangesEnabled = false;

                try
                {
                    int totalFilesCount = 0;
                    const int DbBatchSize = 2000;
                    const int UiBatchSize = 2000;

                    var dbBatchList = new List<FileRecord>();
                    var uiBatchList = new List<FileRecord>();
                    var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var existingPath = new HashSet<string>(
                        await context.Files
                            .Where(f => f.Path.StartsWith(path))
                            .Select(f => f.Path)
                            .ToListAsync(cancellationToken),
                        StringComparer.OrdinalIgnoreCase);

                    var rootDir = new DirectoryInfo(path);
                    var enumerationOptions = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                    };

                    foreach (var file in rootDir.EnumerateFiles("*", enumerationOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        scannedPaths.Add(file.FullName);

                        if (existingPath.Contains(file.FullName)) continue;

                        var record = new FileRecord
                        {
                            Name = file.Name,
                            Path = file.FullName,
                            Size = file.Length,
                            LastModified = file.LastWriteTime,
                            IsDeleted = false
                        };

                        dbBatchList.Add(record);
                        uiBatchList.Add(record);
                        totalFilesCount++;

                        if (uiBatchList.Count >= UiBatchSize)
                        {
                            progress.Report([.. uiBatchList]);
                            uiBatchList.Clear();
                        }

                        if (dbBatchList.Count >= DbBatchSize)
                        {
                            await context.Files.AddRangeAsync(dbBatchList, cancellationToken);
                            await context.SaveChangesAsync(cancellationToken);

                            foreach (var added in dbBatchList) existingPath.Add(added.Path);
                            dbBatchList.Clear();
                        }
                    }

                    if (uiBatchList.Count > 0)
                    {
                        progress.Report([.. uiBatchList]);
                    }

                    if (dbBatchList.Count > 0)
                    {
                        await context.Files.AddRangeAsync(dbBatchList, cancellationToken);
                        await context.SaveChangesAsync(cancellationToken);
                    }

                    var dbFiles = await context.Files
                        .Where(f => f.Path.StartsWith(path))
                        .ToListAsync(cancellationToken);

                    var filesToRemove = dbFiles
                        .Where(f => !scannedPaths.Contains(f.Path))
                        .ToList();

                    if (filesToRemove.Any())
                    {
                        context.RemoveRange(filesToRemove);
                        await context.SaveChangesAsync(cancellationToken);
                    }

                    progress.Report([]);
                }
                finally
                {
                    context.ChangeTracker.AutoDetectChangesEnabled = true;
                }
            }, cancellationToken);
        }
    }
}
