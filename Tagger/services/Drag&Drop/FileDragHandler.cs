using GongSolutions.Wpf.DragDrop;
using System.Windows;
using Tagger.dto;
using Tagger.model;
using Tagger.services.Drag_Drop;
using Tagger.services.interfaces;

namespace Tagger.services
{
    public class FileDragHandler : IDragSource
    {
        private readonly IDialogService _dialogService;

        public FileDragHandler(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public void StartDrag(IDragInfo dragInfo)
        {
            var selectedItems = dragInfo.SourceItems.Cast<FileItemViewModel>().ToList();

            dragInfo.Data = new DraggedObjectsPackage<FileItemViewModel>
            {
                Count = selectedItems.Count,
                Objects = selectedItems
            };

            dragInfo.Effects = DragDropEffects.Link;
        }

        public bool CanStartDrag(IDragInfo dragInfo)
        {
            if (dragInfo.SourceItem == null) return false;

            if (!(dragInfo.SourceItem is FileItemViewModel)) return false;

            return true;
        }

        public void Dropped(IDropInfo dropInfo) { }


        public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }

        void IDragSource.DragCancelled() { }

        public bool TryCatchOccurredException(Exception exception)
        {
            _dialogService.ShowMessage($"Ошибка: {exception.Message}", "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            return true;
        }
    }
}
