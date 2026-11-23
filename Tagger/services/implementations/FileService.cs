using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Windows.Shapes;
using Tagger.db;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class FileService : IFileService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public FileService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<FileRecord>> LoadFirstBatchFilesAsync(string path, CancellationToken token)
        {
            using var context = await _contextFactory.CreateDbContextAsync(token);

            var result = await context.Files
                .AsNoTracking()
                .Include(f => f.Tags)
                .Where(f => f.Path.StartsWith(path)
                            && f.SyncStatus == 0
                            && !f.IsDeleted)
                .Take(3000)
                .ToListAsync(token);

            return result;
        }

        public async Task<List<FileRecord>> LoadFilesForPathInBackgroundAsync(string path, CancellationToken token)
        {
            using var bgContext = await _contextFactory.CreateDbContextAsync();

            return await bgContext.Files
                .AsNoTracking()
                .Include(f => f.Tags)
                .Where(f => f.Path.StartsWith(path))
                .ToListAsync(token);
        }

        public async Task<List<FileRecord>> SearchWithDebounceAsync(
            string searchText,
            string path,
            HashSet<int> tagIds,
            CancellationToken token)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Files
                .AsNoTracking()
                .Include(f => f.Tags)
                .Where(f => f.Path.StartsWith(path) && !f.IsDeleted && f.SyncStatus == 0);

            if (tagIds.Count > 0)
            {
                query = query.Where(f => f.Tags.Any(t => tagIds.Contains(t.Id)));
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string[] keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyword in keywords)
                {
                    string pattern = $"%{keyword}%";
                    query = query.Where(f => EF.Functions.Like(f.Name, pattern));
                }
            }

            token.ThrowIfCancellationRequested();

            var result = await query.Take(3000).ToListAsync(token);

            token.ThrowIfCancellationRequested();

            return result;
        }

        public async Task<List<Tag>> GetTagsByIds(List<int> tagIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var setIds = tagIds.ToHashSet();

            return await context.Tags
                .Where(t => setIds.Contains(t.Id))
                .ToListAsync();
        }

        public async Task DetachTagFromFilesAsync(int tagId, List<int> selectedFileIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.FileTags
                .Where(ft => selectedFileIds.Contains(ft.FileId) && tagId == ft.TagId)
                .ExecuteDeleteAsync();
        }

        public async Task DetachAllTagsFromFilesAsync(int fileId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.FileTags
                .Where(ft => ft.FileId == fileId)
                .ExecuteDeleteAsync();
        }

        public async Task<List<int>> AddFilesFromPathAsync(List<string> paths, IProgress<string> progress)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var fullPaths = paths.Select(System.IO.Path.GetFullPath).Distinct().ToList();

            var existingFiles = await context.Files
                .Where(f => fullPaths.Contains(f.Path))
                .Select(f => ValueTuple.Create(f.Id, f.Path))
                .ToListAsync();

            var existingPathSet = existingFiles.Select(f => f.Item2).ToHashSet();

            var filesToAdd = fullPaths
                .Where(p => !existingPathSet.Contains(p))
                .Select(p => new FileRecord
                {
                    Name = System.IO.Path.GetFileName(p),
                    Path = p,
                    Size = new FileInfo(p).Length,
                    LastModified = File.GetLastWriteTime(p),
                    IsDeleted = false
                })
                .ToList();

            await context.BulkInsertAsync(filesToAdd);

            var ids = await context.Files
                .Where(f => fullPaths.Contains(f.Path))
                .Select(f => f.Id)
                .ToListAsync();

            return ids;

            //var existingFiles = new List<(int Id, string Path)>();
            //const int queryBatchSize = 500;

            //for (int i = 0; i < fullPaths.Count; i += queryBatchSize)
            //{
            //    var currentBatch = fullPaths.Skip(i).Take(queryBatchSize).ToList();

            //    var found = await context.Files
            //        .Where(f => currentBatch.Contains(f.Path))
            //        .Select(f => new { f.Id, f.Path })
            //        .ToListAsync();

            //    foreach (var f in found)
            //        existingFiles.Add((f.Id, f.Path));
            //}

            //var existingPathsSet = existingFiles.Select(f => f.Path).ToHashSet();

            //var newPaths = fullPaths.Where(path => !existingPathsSet.Contains(path)).ToList();

            //if (newPaths.Count == 0)
            //    return existingFiles.Select(f => f.Id).ToList();

            //var filesToAdd = newPaths.Select(path => new FileRecord
            //{
            //    Name = Path.GetFileName(path),
            //    Path = Path.GetFullPath(path),
            //    Size = new FileInfo(path).Length,
            //    LastModified = File.GetLastWriteTime(path),
            //    IsDeleted = false
            //}).ToList();

            //const int savedBatchSize = 100;
            //var insertedIds = new List<int>();

            //for (int i = 0; i < filesToAdd.Count; i += savedBatchSize)
            //{
            //    var batch = filesToAdd.Skip(i).Take(savedBatchSize).ToList();

            //    await context.Files.AddRangeAsync(batch);
            //    await context.SaveChangesAsync();

            //    insertedIds.AddRange(batch.Select(b => b.Id));

            //    progress?.Report($"Добавлено {i + batch.Count()} из {filesToAdd.Count}");
            //}

            //var allIds = existingFiles.Select(f => f.Id).Concat(insertedIds).ToList();

            //return allIds;
        }
    }
}
