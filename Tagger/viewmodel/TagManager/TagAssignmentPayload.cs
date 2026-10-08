using System;
using System.Collections.Generic;
using System.Text;
using Tagger.dto;

namespace Tagger.viewmodel.TagManagerViewModel
{
    public class TagAssignmentPayload
    {
        public List<int> TagIds { get; set; }
        public List<FileItemViewModel> Files { get; set; }

        public TagAssignmentPayload(List<int> tagIds, List<FileItemViewModel> files)
        {
            TagIds = tagIds;
            Files = files;
        }
    }
}
