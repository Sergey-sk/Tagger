using System;
using System.Collections.Generic;
using System.Text;

namespace Tagger.model
{
    public class FileTag
    {
        public int FileId { get; set; }
        public FileRecord File { get; set; } = null!;

        public int TagId { get; set; }
        public Tag Tag { get; set; } = null!;
    }
}
