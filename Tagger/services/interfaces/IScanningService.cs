using System;
using System.Collections.Generic;
using System.Text;
using Tagger.model;

namespace Tagger.services.interfaces
{
    public interface IScanningService
    {
        Task ScanDirectoryAsync(string path, IProgress<List<FileRecord>> progress, CancellationToken cancellationToken);
    }
}
