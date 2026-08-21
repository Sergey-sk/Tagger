using Microsoft.EntityFrameworkCore;
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

            return await context.Files
                .AsNoTracking()
                .Include(f => f.Tags)
                .Where(f => f.Path.StartsWith(path))
                .Take(3000)
                .ToListAsync(token);
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
            List<FileRecord> files,
            string path,
            List<int> tagIds,
            CancellationTokenSource? searchCts)
        {
            searchCts?.Cancel();
            searchCts = new CancellationTokenSource();
            var token = searchCts.Token;

            if (!string.IsNullOrEmpty(searchText))
                await Task.Delay(150, token);

            if (files.Count == 0) return [];

            IEnumerable<FileRecord> query = files
                .Where(f => f.Path.StartsWith(path));

            if (tagIds.Count > 0)
            {
                foreach (var tagId in tagIds)
                {
                    query = query.Where(f => f.Tags.Any(t => t.Id == tagId));
                }
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string[] keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyword in keywords)
                {
                    query = query.Where(f => f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }
            }

            token.ThrowIfCancellationRequested();

            var result = query.Take(3000).ToList();

            token.ThrowIfCancellationRequested();

            return result;
        }

        /// <summary>
        /// Вызывается при перетаскивании или применении файла к тегу
        /// </summary>
        /// <param name="tagId"></param>
        /// <param name="fileIds"></param>
        /// <returns></returns>
        public async Task<Tag?> ApplyFilesToTagsAsync(int tagId, List<int> fileIds)
        {
            if (fileIds == null || fileIds.Count == 0) return null;

            using var context = await _contextFactory.CreateDbContextAsync();

            var tag = await context.Tags
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null) return null;


            const int batchSize = 5000;
            for (int i = 0; i < fileIds.Count; i += batchSize)
            {
                var batchIds = fileIds.Skip(i).Take(batchSize).ToList();

                var taggedFileIds = await context.FileTags
                    .AsNoTracking()
                    .Where(ft => ft.TagId == tagId && batchIds.Contains(ft.FileId))
                    .Select(ft => ft.FileId)
                    .ToListAsync();

                var newIdsForTag = batchIds.Except(taggedFileIds).ToList();

                if (newIdsForTag.Count > 0)
                {
                    var newFileTags = newIdsForTag.Select(fileId => new FileTag
                    {
                        TagId = tagId,
                        FileId = fileId,
                    });

                    context.FileTags.AddRange(newFileTags);
                }
            }

            await context.SaveChangesAsync();
            await Task.Delay(10);

            return tag;
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        public async Task ApplyTagsToFilesAsync(int fileId, List<int> tagIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var fileExist = await context.Files
                .AnyAsync(f => f.Id == fileId);

            if (!fileExist) return;

            var uniqueIncomingIds = tagIds.Distinct().ToList();

            var attachedTagIds = await context.FileTags
                .Where(ft => ft.FileId == fileId)
                .Select(ft => ft.TagId)
                .ToListAsync();

            var existingIdsSet = attachedTagIds.ToHashSet();

            var newTagIds = uniqueIncomingIds
                .Where(id => !existingIdsSet.Contains(id))
                .ToList();

            if (newTagIds.Count == 0) return;

            const int BatchSize = 3000;

            for (int i = 0; i < newTagIds.Count; i += BatchSize)
            {
                var currentBatch = newTagIds.Skip(i).Take(BatchSize);

                var entriesToAdd = new List<FileTag>();

                foreach (var tagId in currentBatch)
                {
                    var link = new FileTag()
                    {
                        TagId = tagId,
                        FileId = fileId,
                    };
                    entriesToAdd.Add(link);
                }

                await context.FileTags.AddRangeAsync(entriesToAdd);
                await context.SaveChangesAsync();
            }
        }
    }
}
