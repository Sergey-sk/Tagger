using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Tagger.model;

namespace Tagger.services.interfaces
{
    public interface IFileService
    {
        Task<List<FileRecord>> LoadFirstBatchFilesAsync(string path, CancellationToken token);
        Task<List<FileRecord>> LoadFilesForPathInBackgroundAsync(string path, CancellationToken token);
        Task<List<FileRecord>> SearchWithDebounceAsync(
            string searchText,
            List<FileRecord> files,
            string path,
            List<int> tagIds,
            CancellationTokenSource? searchCts);
        Task<List<Tag>> GetTagsByIds(List<int> tagIds);
        Task<Tag?> GetTagByIdAsync(int tagId);
    }
}
