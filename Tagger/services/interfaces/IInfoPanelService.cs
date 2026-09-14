using Tagger.dto;

namespace Tagger.services.interfaces
{
    public interface IInfoPanelService
    {
        Task DetachTagFromFilesAsync(int tagId, List<int> fileIds);
        Task<int> AttachTagToFilesAsync(string tagName, List<FileItemViewModel> filesToAttach);
    }
}
