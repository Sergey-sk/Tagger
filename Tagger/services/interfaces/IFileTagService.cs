namespace Tagger.services.interfaces
{
    public interface IFileTagService
    {
        Task<(List<int> addedTagIds, List<int> addedFileIds)> LinkTagsToFileAsync(List<int> fileIds, List<int> tagIds);
        Task<List<int>> LinkFilesToTagAsync(List<int> tagIds, List<int> fileIds);
    }
}
