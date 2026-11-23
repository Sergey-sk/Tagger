using System;
using System.Collections.Generic;
using System.Text;

namespace Tagger.model
{
    public class TempScannedFile
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }
}
