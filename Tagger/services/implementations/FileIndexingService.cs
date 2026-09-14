using Microsoft.EntityFrameworkCore;
using System.IO;
using Tagger.db;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class FileIndexingService : IFileIndexingService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public FileIndexingService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<int>> AddFilesFromPathAsync(List<string> paths, IProgress<string> progress)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var fullPaths = paths.Select(Path.GetFullPath).Distinct().ToList();

            var existingFiles = new List<(int Id, string Path)>();
            const int queryBatchSize = 500;

            for (int i = 0; i < fullPaths.Count; i += queryBatchSize)
            {
                var currentBatch = fullPaths.Skip(i).Take(queryBatchSize).ToList();

                var found = await context.Files
                    .Where(f => currentBatch.Contains(f.Path))
                    .Select(f => new { f.Id, f.Path })
                    .ToListAsync();

                foreach (var f in found)
                    existingFiles.Add((f.Id, f.Path));
            }

            var existingPathsSet = existingFiles.Select(f => f.Path).ToHashSet();

            var newPaths = fullPaths.Where(path => !existingPathsSet.Contains(path)).ToList();

            if (newPaths.Count == 0)
                return existingFiles.Select(f => f.Id).ToList();

            var filesToAdd = newPaths.Select(path => new FileRecord
            {
                Name = Path.GetFileName(path),
                Path = Path.GetFullPath(path),
                Size = new FileInfo(path).Length,
                LastModified = File.GetLastWriteTime(path),
                IsDeleted = false
            }).ToList();

            const int savedBatchSize = 100;
            var insertedIds = new List<int>();

            for (int i = 0; i < filesToAdd.Count; i += savedBatchSize)
            {
                var batch = filesToAdd.Skip(i).Take(savedBatchSize).ToList();

                await context.Files.AddRangeAsync(batch);
                await context.SaveChangesAsync();

                insertedIds.AddRange(batch.Select(b => b.Id));

                progress?.Report($"Добавлено {i + batch.Count()} из {filesToAdd.Count}");
            }

            var allIds = existingFiles.Select(f => f.Id).Concat(insertedIds).ToList();

            return allIds;
        }
    }
}
