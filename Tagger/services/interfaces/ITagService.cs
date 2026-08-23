using System.Collections.ObjectModel;
using System.ComponentModel;
using Tagger.model;

namespace Tagger.services.interfaces
{
    public interface ITagService
    {
        Task<List<Tag>> LoadTagsAsync();
        Task<Tag> CreateTagAsync(string tagName);
        Task RemoveTagAsync(int tagId);
        Task<List<int>> GetFileIdsByPathAsync(List<string> paths);
        Task<List<FileRecord>> GetFilesByIdsAsync(List<int> fileIds);
        Task<int> GetFilesCountByTagId(int tagId);
    }
}
