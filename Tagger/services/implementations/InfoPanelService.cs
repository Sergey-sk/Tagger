using CommunityToolkit.Mvvm.Messaging;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Tagger.db;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class InfoPanelService : IInfoPanelService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITagService _tagService;

        public InfoPanelService(IDbContextFactory<ApplicationDbContext> contextFactory, ITagService tagService)
        {
            _contextFactory = contextFactory;
            _tagService = tagService;
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
                var requestMessage = new AddTagMessage(tagName);
                await WeakReferenceMessenger.Default.Send(requestMessage);

                bool isSuccess = await requestMessage.Response;
                if (!isSuccess)
                    return (-1, [-1]);

                tag = await context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                if (tag == null) return (-1, [-1]);
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

            //const int batchSize = 500;
            //for (int i = 0; i < fileTags.Count; i += batchSize)
            //{
            //    var currentBatch = fileTags.Skip(i).Take(batchSize).ToList();

            //    await context.FileTags.AddRangeAsync(currentBatch);
            //    await context.SaveChangesAsync();
            //}

            return (tag.Id, addedFileIds);
        }
    }
}
