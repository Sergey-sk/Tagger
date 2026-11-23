using Tagger.viewmodel.FileViewModels;

namespace Tagger.viewmodel.TagManager
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
