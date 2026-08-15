using System;
using System.Collections.Generic;
using System.Text;

namespace Tagger.model
{
    public class SavedSearch
    {
        public int Id { get; set; }
        public string QueryText { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
    }
}
