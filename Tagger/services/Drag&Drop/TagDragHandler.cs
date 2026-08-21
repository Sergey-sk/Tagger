using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using System.Windows;
using Tagger.messages;
using Tagger.model;
using Tagger.services.Drag_Drop;
using Tagger.services.interfaces;

namespace Tagger.services
{
    public class TagDragHandler : IDragSource
    {
        private readonly IDialogService _dialogService;

        public TagDragHandler(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public void StartDrag(IDragInfo dragInfo)
        {
            var selectedTags = dragInfo.SourceItems.Cast<Tag>().ToList();

            dragInfo.Data = new DraggedObjectsPackage<Tag>
            {
                Count = selectedTags.Count,
                Objects = selectedTags
            };

            dragInfo.Effects = DragDropEffects.Link;

            WeakReferenceMessenger.Default.Send(new RemoveSelectedTag(selectedTags));
        }

        public bool CanStartDrag(IDragInfo dragInfo)
        {
            if (dragInfo.SourceItem == null) return false;

            if (!(dragInfo.SourceItem is Tag)) return false;

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
