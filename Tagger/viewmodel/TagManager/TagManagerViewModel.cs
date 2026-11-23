using CommunityToolkit.Mvvm.ComponentModel;
using GongSolutions.Wpf.DragDrop;
using System.Collections.ObjectModel;
using Tagger.services;
using Tagger.services.interfaces;
using Tagger.viewmodel.TagManager;

namespace Tagger.viewmodel.TagManagerViewModel
{
    public partial class TagManagerViewModel : ObservableObject
    {
        public IDragSource DragHandler { get; }
        public IDropTarget DropHandler { get; }

        public TagListViewModel TagList { get; }
        public TagSelectionFilterViewModel TagSelectionFilter { get; }
        public TagCrudActionsViewModel TagCrudActions { get; }

        public TagManagerViewModel(TagListViewModel tagList,
                                   TagSelectionFilterViewModel tagSelectionFilter,
                                   TagCrudActionsViewModel tagCrudActions,
                                   IDialogService dialogService,
                                   ITagService tagService,
                                   ITagStateContext tagStateContext,
                                   IFileTagService fileTagService,
                                   IFileService fileService)
        {
            TagList = tagList;
            TagSelectionFilter = tagSelectionFilter;
            TagCrudActions = tagCrudActions;

            DragHandler = new TagDragHandler(dialogService, tagStateContext);
            DropHandler = new TagDropHandler(fileTagService, fileService, dialogService, TagList);
        }
    }
}
