using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tagger.messages;
using Tagger.model;
using Tagger.services.Drag_Drop;
using Tagger.services.interfaces;
using Tagger.viewmodel;
using Tagger.viewmodel.FileViewModels;

namespace Tagger.services
{
    public class FileDropHandler : IDropTarget
    {
        private readonly IFileTagService _fileTagService;
        private readonly IFileService _fileService;
        private readonly IDialogService _dialogService;
        private readonly IFileCollectionController _fileCollectionController;

        public FileDropHandler(IFileTagService fileTagService,
                               IFileService fileService,
                               IDialogService dialogService,
                               IFileCollectionController fileCollectionController)
        {
            _fileTagService = fileTagService;
            _fileService = fileService;
            _dialogService = dialogService;
            _fileCollectionController = fileCollectionController;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            bool canDrop = false;

            var targetFile = dropInfo.TargetItem as FileItemViewModel;
            var tagsControl = dropInfo.VisualTarget as Selector;

            if (targetFile == null && tagsControl == null)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            if (dropInfo.Data is DraggedObjectsPackage<TagItemViewModel> tags && tags.Count > 0)
                canDrop = true;

            if (canDrop)
            {
                dropInfo.Effects = DragDropEffects.Link;

                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
            }
            else
                dropInfo.Effects = DragDropEffects.None;
        }

        public void Drop(IDropInfo dropInfo)
        {
            var targetFile = dropInfo.TargetItem as FileItemViewModel;
            if (targetFile == null) return;

            DraggedObjectsPackage<TagItemViewModel>? tags = null;

            if (dropInfo.Data is DraggedObjectsPackage<TagItemViewModel> draggedTags)
            {
                tags = draggedTags;
            }
            else if (dropInfo.Data is DataObject data)
            {
                tags = data.GetData(typeof(DraggedObjectsPackage<TagItemViewModel>)) as DraggedObjectsPackage<TagItemViewModel>;
            }

            if (tags == null || tags.Count == 0) return;

            var filesToApply = new List<FileItemViewModel>();

            if (dropInfo.VisualTarget is ListView listView &&
                listView.SelectedItems.Count > 1 &&
                listView.SelectedItems.Contains(targetFile))
            {
                filesToApply.AddRange(listView.SelectedItems.Cast<FileItemViewModel>());
            }

            if (filesToApply.Count == 0)
                filesToApply.Add(targetFile);

            var fileIds = filesToApply.Select(f => f.Id).ToList();
            _ = ApplyTagsToFilesAsync(fileIds, tags);
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        private async Task ApplyTagsToFilesAsync(List<int> fileIds, DraggedObjectsPackage<TagItemViewModel> uiTags)
        {
            try
            {
                var tagIds = uiTags.Objects.Select(t => t.Id).ToList();
                var (addedTagIds, addedFileIds) = await _fileTagService.LinkTagsToFileAsync([.. fileIds], [.. tagIds]);
                var tags = await _fileService.GetTagsByIds(tagIds);

                _fileCollectionController.AppendTagsToUiFiles(fileIds, uiTags.Objects, tags);

                WeakReferenceMessenger.Default.Send(new ApplyFileMessage(addedTagIds, addedFileIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
