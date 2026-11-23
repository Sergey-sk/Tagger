using GongSolutions.Wpf.DragDrop;
using System.Windows;
using Tagger.services.Drag_Drop;
using Tagger.services.interfaces;
using Tagger.viewmodel;

namespace Tagger.services
{
    public class TagDragHandler : IDragSource
    {
        private readonly IDialogService _dialogService;
        private readonly ITagStateContext _tagStateContext;

        public TagDragHandler(IDialogService dialogService, ITagStateContext tagStateContext)
        {
            _dialogService = dialogService;
            _tagStateContext = tagStateContext;
        }

        public void StartDrag(IDragInfo dragInfo)
        {
            var selectedTags = dragInfo.SourceItems.Cast<TagItemViewModel>().ToList();

            dragInfo.Data = new DraggedObjectsPackage<TagItemViewModel>
            {
                Count = selectedTags.Count,
                Objects = selectedTags
            };

            dragInfo.Effects = DragDropEffects.Link;

            _tagStateContext.RemoveSelectedTags(selectedTags);
        }

        public bool CanStartDrag(IDragInfo dragInfo)
        {
            if (dragInfo.SourceItem == null) return false;

            if (!(dragInfo.SourceItem is TagItemViewModel)) return false;

            return true;
        }

        public void Dropped(IDropInfo dropInfo) { }

        public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }

        public void DragCancelled() { }

        public bool TryCatchOccurredException(Exception exception)
        {
            _dialogService.ShowMessage($"Ошибка: {exception.Message}", "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            return true;
        }
    }
}
