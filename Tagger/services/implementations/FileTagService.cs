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
        /// <param name="fileId"></param>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        public async Task<List<int>> LinkTagsToFileAsync(int fileId, List<int> tagIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var fileExist = await context.Files
                .AnyAsync(f => f.Id == fileId);

            if (!fileExist) return [];

            var uniqueIncomingIds = tagIds.Distinct().ToList();

            var attachedTagIds = await context.FileTags
                .Where(ft => ft.FileId == fileId && uniqueIncomingIds.Contains(ft.TagId))
                .Select(ft => ft.TagId)
                .ToListAsync();

            var existingIdsSet = attachedTagIds.ToHashSet();

            var newTagIds = uniqueIncomingIds
                .Where(id => !existingIdsSet.Contains(id))
                .ToList();

            if (newTagIds.Count == 0) return [];

            const int BatchSize = 5000;

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

            return newTagIds;
        }
    }
}
