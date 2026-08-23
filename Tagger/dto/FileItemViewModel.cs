using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Tagger.model;
using Tagger.viewmodel;

namespace Tagger.dto
{
    public partial class FileItemViewModel : ObservableObject
    {
        public FileRecord Model { get; }

        public int Id => Model.Id;
        public string Name => Model.Name;

        public string Path => Model.Path;
        public long Size => Model.Size;
        public DateTime LastModified => Model.LastModified;
        public ObservableCollection<TagItemViewModel> Tags { get; } = new();

        public FileItemViewModel(FileRecord model, Dictionary<int, TagItemViewModel> allUiTags)
        {
            Model = model;

            foreach (var tag in model.Tags)
            {
                if (allUiTags.TryGetValue(tag.Id, out var existingUiTag))
                    Tags.Add(existingUiTag);
                else
                {
                    var newUiTag = new TagItemViewModel(tag);
                    allUiTags[tag.Id] = newUiTag;
                    Tags.Add(newUiTag);
                }
            }
        }
    }
}
