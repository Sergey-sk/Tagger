using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.dto;
using Tagger.model;
using Tagger.viewmodel.TagManagerViewModel;

namespace Tagger.viewmodel
{
    public partial class TagItemViewModel : ObservableObject
    {
        public Tag Model { get; }
        private readonly Func<List<FileItemViewModel>> _getSelectedFiles;

        public int Id => Model.Id;
        public string Name => Model.Name;

        [ObservableProperty]
        public partial int FilesCount { get; private set; }

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isAttachedToSelectedFiles;

        public TagItemViewModel(Tag model, Func<List<FileItemViewModel>> getSelectedFiles)
        {
            Model = model;
            _getSelectedFiles = getSelectedFiles;
            FilesCount = model.Files.Count;
        }

        public TagAssignmentPayload AssignmentPayload => new TagAssignmentPayload(
            tagIds: [Id],
            files: _getSelectedFiles.Invoke()
        );

        public void IncrementFilesCount(int num = 1) => FilesCount += num;
        public void DecrementFilesCount() => FilesCount--;
    }
}
