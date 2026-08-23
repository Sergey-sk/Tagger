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

        public async Task<int> GetFilesCountByTagId(int tagId)
        {
            var context = await _contextFactory.CreateDbContextAsync();
            return await context.Tags
                .Where(t => t.Id == tagId)
                .Select(t => t.Files.Count)
                .FirstOrDefaultAsync();
        }
    }
}
