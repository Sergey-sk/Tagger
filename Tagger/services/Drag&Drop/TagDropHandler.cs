using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tagger.messages;
using Tagger.model;
using Tagger.services.Drag_Drop;
using Tagger.services.implementations;
using Tagger.services.interfaces;
using Tagger.viewmodel;
using Tagger.viewmodel.FileViewModels;
using Tagger.viewmodel.TagManager;

namespace Tagger.services
{
    public class TagDropHandler : IDropTarget
    {
        private readonly IFileTagService _fileTagService;
        private readonly IFileService _fileService;
        private readonly IDialogService _dialogService;
        private readonly ITagCollectionController _tagCollectionController;

        public TagDropHandler(IFileTagService fileTagService,
                              IFileService fileService,
                              IDialogService dialogService,
                              ITagCollectionController tagCollectionController)
        {
            _fileTagService = fileTagService;
            _fileService = fileService;
            _dialogService = dialogService;
            _tagCollectionController = tagCollectionController;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            bool canDrop = false;

            var targetTag = dropInfo.TargetItem as TagItemViewModel;
            var tagsControl = dropInfo.VisualTarget as Selector;

            if (targetTag == null && tagsControl == null)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            if (dropInfo.Data is DraggedObjectsPackage<FileItemViewModel> files && files.Count > 0)
                canDrop = true;

            if (dropInfo.Data is DataObject data && data.ContainsFileDropList())
                canDrop = true;

            if (dropInfo.Data is string[] paths && paths.All(File.Exists))
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
            var targetTag = dropInfo.TargetItem as TagItemViewModel;
            if (targetTag == null) return;

            var tagsToApply = new List<TagItemViewModel>();

            if (dropInfo.VisualTarget is ListBox listBox &&
                listBox.SelectedItems.Count > 1 &&
                listBox.SelectedItems.Contains(targetTag))
            {
                tagsToApply.AddRange(listBox.SelectedItems.Cast<TagItemViewModel>());
            }
            else
                tagsToApply.Add(targetTag);

            var tagIds = tagsToApply.Select(t => t.Id).ToList();

            if (dropInfo.Data is DraggedObjectsPackage<FileItemViewModel> draggedFiles)
                _ = ApplyTagToFilesAsync(tagIds, [.. draggedFiles.Objects.Select(f => f.Id)]);

            if (dropInfo.Data is DataObject data && data.ContainsFileDropList())
            {
                var paths = data.GetFileDropList().Cast<string>().ToArray();
                _ = HandleExternalDropAsync(tagIds, paths);
            }

            if (dropInfo.Data is string[] filePaths)
                _ = HandleExternalDropAsync(tagIds, filePaths);
        }

        private async Task ApplyTagToFilesAsync(List<int> tagIds, List<int> fileIds)
        {
            try
            {
                var newFileIds = await _fileTagService.LinkFilesToTagAsync([.. tagIds], [.. fileIds]);

                _tagCollectionController.AppendFilesToUiTags(tagIds, newFileIds.Count);

                WeakReferenceMessenger.Default.Send(new TagsForSelectionChangedMessage());
                WeakReferenceMessenger.Default.Send(new ApplyTagMessage(tagIds, fileIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task HandleExternalDropAsync(List<int> tagIds, string[] filePaths)
        {
            try
            {
                bool isOperationActive = true;

                var progress = new Progress<string>(msg =>
                {
                    if (isOperationActive)
                        WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(true, msg));
                });

                var fileIds = await _fileService.AddFilesFromPathAsync(filePaths.ToList(), progress);

                var newFileIds = await _fileTagService.LinkFilesToTagAsync([.. tagIds], [.. fileIds]);

                _tagCollectionController.AppendFilesToUiTags(tagIds, newFileIds.Count);

                isOperationActive = false;

                WeakReferenceMessenger.Default.Send(new ApplyTagMessage(tagIds, fileIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
