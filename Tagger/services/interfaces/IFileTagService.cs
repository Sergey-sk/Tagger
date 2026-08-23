namespace Tagger.services.interfaces
{
    public interface IFileTagService
    {
        Task<List<int>> LinkTagsToFileAsync(int fileId, List<int> tagIds);
        Task<List<int>> LinkFilesToTagAsync(int tagId, List<int> fileIds);
    }
}
