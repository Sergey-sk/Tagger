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
        public async Task<List<int>> LinkFilesToTagAsync(List<int> tagIds, List<int> fileIds)
        {
            if (tagIds == null || tagIds.Count == 0 || fileIds == null || fileIds.Count == 0) return [];

            using var context = await _contextFactory.CreateDbContextAsync();

            var addedFileIds = new HashSet<int>();
            var newFileTags = new List<FileTag>();

            const int SearchBatchSize = 500;

            foreach(var currentBatch in fileIds.Chunk(SearchBatchSize))
            {
                var existingSet = await context.FileTags
                    .AsNoTracking()
                    .Where(ft => currentBatch.Contains(ft.FileId) && tagIds.Contains(ft.TagId))
                    .Select(ft => ValueTuple.Create(ft.TagId, ft.FileId))
                    .ToHashSetAsync();

                foreach(var fileId in currentBatch)
                {
                    foreach(var tagId in tagIds)
                    {
                        if(!existingSet.Contains((tagId, fileId)))
                        {
                            newFileTags.Add(new FileTag { TagId = tagId, FileId = fileId });
                            addedFileIds.Add(fileId);
                        }

                        if(newFileTags.Count >= 2000)
                        {
                            context.FileTags.AddRange(newFileTags);
                            await context.SaveChangesAsync();

                            context.ChangeTracker.Clear();
                            newFileTags.Clear();
                        }
                    }
                }
            }

            if(newFileTags.Count > 0)
            {
                context.FileTags.AddRange(newFileTags);
                await context.SaveChangesAsync();

                context.ChangeTracker.Clear();
            }

            return addedFileIds.ToList();
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
