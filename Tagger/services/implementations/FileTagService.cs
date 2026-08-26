using Microsoft.EntityFrameworkCore;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class FileTagService : IFileTagService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public FileTagService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Вызывается при перетаскивании или применении файла к тегу
        /// </summary>
        /// <param name="tagId"></param>
        /// <param name="fileIds"></param>
        /// <returns></returns>
        public async Task<List<int>> LinkFilesToTagAsync(int tagId, List<int> fileIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var tagExist = await context.Tags
                .AnyAsync(t => t.Id == tagId);

            if (!tagExist) return [];

            var uniqueIncomingIds = fileIds.Distinct().ToList();

            var attachedFileIds = await context.FileTags
                .Where(ft => ft.TagId == tagId && uniqueIncomingIds.Contains(ft.FileId))
                .Select(ft => ft.FileId)
                .ToListAsync();

            var existingIdsSet = attachedFileIds.ToHashSet();

            var newFileIds = uniqueIncomingIds
                .Where(id => !existingIdsSet.Contains(id))
                .ToList();

            if (newFileIds.Count == 0) return [];

            const int BatchSize = 5000;

            for (int i = 0; i < newFileIds.Count; i += BatchSize)
            {
                var currentBatch = newFileIds.Skip(i).Take(BatchSize);
                var entriesToAdd = new List<FileTag>();

                foreach (var fileId in currentBatch)
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

            return newFileIds;
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="fileIds"></param>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        public async Task<(List<int> addedTagIds, List<int> addedFileIds)> LinkTagsToFileAsync(List<int> fileIds, List<int> tagIds)
        {
            if (fileIds == null || fileIds.Count == 0 || tagIds == null || tagIds.Count == 0) return ([], []);

            using var context = await _contextFactory.CreateDbContextAsync();

            var addedElementIds = new HashSet<(int, int)>();
            var newFileTags = new List<FileTag>();

            const int SearchBatchSize = 500;

            foreach (int[] currentBatch in fileIds.Chunk(SearchBatchSize))
            {
                var existingSet = await context.FileTags
                    .AsNoTracking()
                    .Where(ft => currentBatch.Contains(ft.FileId) && tagIds.Contains(ft.TagId))
                    .Select(ft => ValueTuple.Create(ft.FileId, ft.TagId))
                    .ToHashSetAsync();

                foreach (var fileId in currentBatch)
                {
                    foreach (var tagId in tagIds)
                    {
                        if (!existingSet.Contains((fileId, tagId)))
                        {
                            newFileTags.Add(new FileTag { FileId = fileId, TagId = tagId });
                            addedElementIds.Add((tagId, fileId));
                        }

                        if (newFileTags.Count >= 2000)
                        {
                            context.FileTags.AddRange(newFileTags);
                            await context.SaveChangesAsync();
                            context.ChangeTracker.Clear();
                            newFileTags.Clear();
                        }
                    }
                }
            }

            if (newFileTags.Count > 0)
            {
                context.FileTags.AddRange(newFileTags);
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
            }

            var uniqueAddedTags = new HashSet<int>();
            var uniqueAddedFiles = new HashSet<int>();

            foreach (var addedElementId in addedElementIds)
            {
                uniqueAddedTags.Add(addedElementId.Item1);
                uniqueAddedFiles.Add(addedElementId.Item2);
            }

            return (uniqueAddedTags.ToList(), uniqueAddedFiles.ToList());
        }
    }
}
