namespace Tagger.services.interfaces
{
    public interface IInfoPanelService
    {
        Task DetachTagFromFilesAsync(int tagId, List<int> fileIds);
        Task<(int tagId, List<int> fileIds)> AttachTagToFilesAsync(string tagName, List<int> fileIdsToAttach);
    }
}
