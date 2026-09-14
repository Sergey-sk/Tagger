using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Tagger.dto;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class InfoPanelService : IInfoPanelService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public InfoPanelService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        public async Task DetachTagFromFilesAsync(int tagId, List<int> fileIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.FileTags
                .Where(ft => ft.TagId == tagId && fileIds.Contains(ft.FileId))
                .ExecuteDeleteAsync();
        }

        public async Task<int> AttachTagToFilesAsync(string tagName, List<FileItemViewModel> filesToAttach)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var tag = await context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);

            if (tag == null)
            {
                var requestMessage = new AddTagMessage(tagName);
                await WeakReferenceMessenger.Default.Send(requestMessage);

                bool isSuccess = await requestMessage.Response;
                if (!isSuccess)
                    return -1;

                tag = await context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                if (tag == null) return -1;
            }

            var fileIds = filesToAttach.Select(f => f.Id).ToHashSet();
            var existingFileTags = await context.FileTags
                .Where(ft => ft.TagId == tag.Id && fileIds.Contains(ft.FileId))
                .Select(ft => ValueTuple.Create(ft.TagId, ft.FileId))
                .ToHashSetAsync();

            List<FileTag> fileTags = [];

            foreach (var file in filesToAttach)
            {
                var newFileTag = ValueTuple.Create(tag.Id, file.Id);
                if (!existingFileTags.Contains(newFileTag))
                    fileTags.Add(new FileTag { TagId = newFileTag.Item1, FileId = newFileTag.Item2 });
            }

            const int batchSize = 500;
            for (int i = 0; i < fileTags.Count; i += batchSize)
            {
                var currentBatch = fileTags.Skip(i).Take(batchSize).ToList();

                await context.FileTags.AddRangeAsync(currentBatch);
                await context.SaveChangesAsync();
            }

            return tag.Id;
        }
    }
}
