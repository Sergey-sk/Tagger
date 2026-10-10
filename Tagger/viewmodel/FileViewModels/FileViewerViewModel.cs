using CommunityToolkit.Mvvm.ComponentModel;
using GongSolutions.Wpf.DragDrop;
using Tagger.services;
using Tagger.services.interfaces;
using Tagger.viewmodel.FileViewModels;
using Tagger.viewmodel.Search;

namespace Tagger.viewmodel.FileViewerViewModel
{
    public partial class FileViewerViewModel : ViewModelBase
    {
        public SearchViewModel SearchBox { get; }
        public FileGridViewModel Grid { get; }
        public FolderWorkspaceViewModel Workspace { get; }

        public IDragSource DragHandler { get; }
        public IDropTarget DropHandler { get; }

        [ObservableProperty]
        private bool _isPanelOpen = false;


        public FileViewerViewModel(SearchViewModel searchBox,
                                   FileGridViewModel grid,
                                   FolderWorkspaceViewModel workspace,
                                   IDialogService dialogService,
                                   IFileService fileService,
                                   IFileTagService fileTagService)
        {
            SearchBox = searchBox;
            Grid = grid;
            Workspace = workspace;

            DragHandler = new FileDragHandler(dialogService);
            DropHandler = new FileDropHandler(fileTagService, fileService, dialogService, Grid);
        }
    }
}