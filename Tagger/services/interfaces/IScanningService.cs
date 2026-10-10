using System;
using System.Collections.Generic;
using System.Text;
using Tagger.model;

namespace Tagger.services.interfaces
{
    public interface IScanningService
    {
        Task ScanDirectoryAsync(string path, string lastSuccessScanTime,IProgress<int> progress, CancellationToken cancellationToken);
    }
}
