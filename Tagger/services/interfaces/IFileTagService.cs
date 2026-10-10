namespace Tagger.services.interfaces
{
    public interface IFileTagService
    {
        Task<(List<int> addedTagIds, List<int> addedFileIds)> LinkTagsToFileAsync(HashSet<int> fileIds, HashSet<int> tagIds);
        Task<List<int>> LinkFilesToTagAsync(HashSet<int> tagIds, HashSet<int> fileIds);
    }
}
