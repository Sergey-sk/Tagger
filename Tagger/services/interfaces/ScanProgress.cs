using System;
using System.Collections.Generic;
using System.Text;

namespace Tagger.services.interfaces
{
    public class ScanProgress
    {
        public int TotalScanned { get; set; }
        public int AddedFiles { get; set; }
        public int RemovedFiles { get; set; }
        public string Status { get; set; }
    }
}
