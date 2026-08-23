using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.model;

namespace Tagger.viewmodel
{
    public partial class TagItemViewModel : ObservableObject
    {
        public Tag Model { get; }

        public int Id => Model.Id;
        public string Name => Model.Name;

        [ObservableProperty]
        public partial int FilesCount { get; private set; }

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isAttachedToSelectedFiles;

        public TagItemViewModel(Tag model)
        {
            Model = model;
            FilesCount = model.Files.Count;
        }

        public void IncrementFilesCount() => FilesCount++;
        public void DecrementFilesCount() => FilesCount--;
    }
}
