using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Tagger.db;
using Tagger.model;
using Tagger.services.interfaces;
using Tagger.viewmodel;

namespace Tagger.services.implementations
{
    public class InfoPanelService : IInfoPanelService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITagService _tagService;
        private readonly ITagStateContext _tagStateContext;

        public InfoPanelService(IDbContextFactory<ApplicationDbContext> contextFactory,
                                ITagService tagService,
                                ITagStateContext tagStateContext)
        {
            _contextFactory = contextFactory;
            _tagService = tagService;
            _tagStateContext = tagStateContext;
        }
        public async Task DetachTagFromFilesAsync(int tagId, List<int> fileIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.FileTags
                .Where(ft => ft.TagId == tagId && fileIds.Contains(ft.FileId))
                .ExecuteDeleteAsync();
        }

        public async Task<(int tagId, List<int> fileIds)> AttachTagToFilesAsync(string tagName, List<int> fileIdsToAttach)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var tag = await context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);

            if (tag == null)
            {
                try
                {
                    tag = await _tagService.CreateTagAsync(tagName);

                    _tagStateContext.AddTag(tag);
                }
                catch (ArgumentException)
                {
                    return (-1, [-1]);
                }
            }

            var fileIds = fileIdsToAttach.ToHashSet();

            var existingFileIds = await context.FileTags
                .AsNoTracking()
                .Where(ft => ft.TagId == tag.Id && fileIds.Contains(ft.FileId))
                .Select(ft => ft.FileId)
                .ToHashSetAsync();

            List<FileTag> fileTags = [];
            List<int> addedFileIds = [];

            foreach (var file in fileIdsToAttach)
            {
                if (!existingFileIds.Contains(file))
                {
                    fileTags.Add(new FileTag { FileId = file, TagId = tag.Id });
                    addedFileIds.Add(file);
                }
            }

            await context.BulkInsertAsync(fileTags);

            return (tag.Id, addedFileIds);
        }
    }
}
