using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class TagService : ITagService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public TagService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Tag>> LoadTagsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var collection = await context.Tags
                    .Include(t => t.Files)
                    .ToListAsync();

            foreach (var tag in collection)
            {
                var validFiles = tag.Files
                    .Where(f => File.Exists(f.Path))
                    .ToList();

                if (tag.Files.Count != validFiles.Count)
                {
                    tag.Files = validFiles;
                    context.Tags.Update(tag);
                }
            }

            await context.SaveChangesAsync();

            return collection.OrderByDescending(t => t.Files.Count).ToList();
        }

        public async Task<Tag> CreateTagAsync(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                throw new ArgumentException("Имя не может быть пустым", nameof(tagName));

            using var context = await _contextFactory.CreateDbContextAsync();

            var exist = await context.Tags
                .AsNoTracking()
                .AnyAsync(t => t.Name.ToLower() == tagName.ToLower());

            if (exist)
                throw new ArgumentException("Тег с таким именем уже существует", nameof(tagName));

            var newTag = new Tag { Name = tagName };

            context.Tags.Add(newTag);
            await context.SaveChangesAsync();

            return newTag;
        }

        public async Task RemoveTagAsync(int tagId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.Tags
                .Where(t => t.Id == tagId)
                .ExecuteDeleteAsync();
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        public async Task<FileRecord?> ApplyTagsToFilesAsync(int fileId, List<int> tagIds)
        {
            if (tagIds == null || tagIds.Count == 0) return null;

            using var context = await _contextFactory.CreateDbContextAsync();

            var file = await context.Files
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == fileId);

            if (file == null) return null;


            const int batchSize = 5000;
            for (int i = 0; i < tagIds.Count; i += batchSize)
            {
                var batchIds = tagIds.Skip(i).Take(batchSize).ToList();

                var taggedTagIds = await context.FileTags
                    .AsNoTracking()
                    .Where(ft => ft.FileId == fileId && batchIds.Contains(ft.TagId))
                    .Select(ft => ft.TagId)
                    .ToListAsync();

                var newIdsForFile = batchIds.Except(taggedTagIds).ToList();

                if (newIdsForFile.Count > 0)
                {
                    var newFileTags = newIdsForFile.Select(tagId => new FileTag
                    {
                        TagId = tagId,
                        FileId = fileId,
                    }).ToList();

                    context.FileTags.AddRange(newFileTags);
                }
            }

            await context.SaveChangesAsync();
            await Task.Delay(10);

            return file;
        }

        /// <summary>
        /// Вызывается при применении/перетаскивании файла на тег
        /// </summary>
        /// <param name="tagId"></param>
        /// <param name="fileIds"></param>
        /// <returns></returns>
        public async Task ApplyFilesToTagsAsync(int tagId, List<int> fileIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var tagExist = await context.Tags
                .AnyAsync(t => t.Id == tagId);

            if (!tagExist) return;

            var uniqueIncomingIds = fileIds.Distinct().ToList();

            var attachedFileIds = await context.FileTags
                .Where(ft => ft.TagId == tagId)
                .Select(ft => ft.FileId)
                .ToListAsync();

            var existingIdsSet = attachedFileIds.ToHashSet();

            var newFileIds = uniqueIncomingIds
                .Where(id => !existingIdsSet.Contains(id))
                .ToList();

            if (newFileIds.Count == 0) return;

            const int BatchSize = 3000;

            for (int i = 0; i < newFileIds.Count; i+=BatchSize)
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
        }

        public async Task<List<int>> GetFileIdsByPathAsync(List<string> paths)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Files
                .Where(f => paths.Contains(f.Path))
                .Select(f => f.Id)
                .ToListAsync();
        }

        public async Task<List<FileRecord>> GetFilesByIdsAsync(List<int> fileIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Files
                .Where(f => fileIds.Contains(f.Id))
                .ToListAsync();
        }
    }
}
