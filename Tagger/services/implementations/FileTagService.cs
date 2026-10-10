using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Tagger.db;
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
        /// <param name="tagIds"></param>
        /// <param name="fileIds"></param>
        /// <returns></returns>
        public async Task<List<int>> LinkFilesToTagAsync(HashSet<int> tagIds, HashSet<int> fileIds)
        {
            if (tagIds == null || tagIds.Count == 0 || fileIds == null || fileIds.Count == 0) return [];

            using var context = await _contextFactory.CreateDbContextAsync();

            var existingSet = await context.FileTags
                .AsNoTracking()
                .Where(ft => fileIds.Contains(ft.FileId) && tagIds.Contains(ft.TagId))
                .Select(ft => ValueTuple.Create(ft.FileId, ft.TagId))
                .ToHashSetAsync();

            List<FileTag> fileTagsToInsert = [];
            var addedFileIds = new HashSet<int>();

            foreach (var fileId in fileIds)
            {
                foreach (var tagId in tagIds)
                {
                    if (!existingSet.Contains(ValueTuple.Create(fileId, tagId)))
                    {
                        fileTagsToInsert.Add(new FileTag { FileId = fileId, TagId = tagId });

                        addedFileIds.Add(fileId);
                    }
                }
            }

            await context.BulkInsertOrUpdateAsync(fileTagsToInsert);

            return addedFileIds.ToList();
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="fileIds"></param>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        public async Task<(List<int> addedTagIds, List<int> addedFileIds)> LinkTagsToFileAsync(HashSet<int> fileIds, HashSet<int> tagIds)
        {
            if (fileIds == null || fileIds.Count == 0 || tagIds == null || tagIds.Count == 0) return ([], []);

            using var context = await _contextFactory.CreateDbContextAsync();

            var existingSet = await context.FileTags
                .AsNoTracking()
                .Where(ft => fileIds.Contains(ft.FileId) && tagIds.Contains(ft.TagId))
                .Select(ft => ValueTuple.Create(ft.FileId, ft.TagId))
                .ToHashSetAsync();

            List<FileTag> fileTagsToInsert = [];
            var uniqueAddedTags = new HashSet<int>();
            var uniqueAddedFiles = new HashSet<int>();

            foreach(var fileId in fileIds)
            {
                foreach(var tagId in tagIds)
                {
                    if (!existingSet.Contains(ValueTuple.Create(fileId, tagId)))
                    {
                        fileTagsToInsert.Add(new FileTag { FileId = fileId, TagId = tagId});

                        uniqueAddedFiles.Add(fileId);
                        uniqueAddedTags.Add(tagId);
                    }
                }
            }

            await context.BulkInsertOrUpdateAsync(fileTagsToInsert);

            return (uniqueAddedTags.ToList(), uniqueAddedFiles.ToList());
        }
    }
}
