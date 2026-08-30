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

        public bool IsAttachedToAllSelectedFiles
        {
            get
            {
                var files = _getSelectedFiles.Invoke();
                if (files == null || files.Count == 0) return false;

                return files.All(f => f.Tags.Any(t => t.Id == Id));
            }
        }

        public bool IsAttachedToAnySelectedFiles
        {
            get
            {
                var files = _getSelectedFiles.Invoke();
                if (files == null || files.Count == 0) return false;

                return files.Any(f => f.Tags.Any(t => t.Id == Id));
            }
        }

        public bool IsSelectionModeActive => _getSelectedFiles.Invoke().Count > 0;

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
        public void DecrementFilesCount(int num = 1) => FilesCount -= num;

        public void RefreshSelectionState()
        {
            OnPropertyChanged(nameof(IsSelectionModeActive));
            OnPropertyChanged(nameof(AssignmentPayload));
            OnPropertyChanged(nameof(IsAttachedToAllSelectedFiles));
            OnPropertyChanged(nameof(IsAttachedToAnySelectedFiles));
        }
    }
}
