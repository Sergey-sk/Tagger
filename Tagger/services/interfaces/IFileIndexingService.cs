using System;
using System.Collections.Generic;
using System.Text;

namespace Tagger.services.interfaces
{
    public interface IFileIndexingService
    {
        Task<List<int>> AddFilesFromPathAsync(List<string> paths, IProgress<string> progress);
    }
}
