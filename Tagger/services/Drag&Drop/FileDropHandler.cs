using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tagger.dto;
using Tagger.messages;
using Tagger.model;
using Tagger.services.Drag_Drop;
using Tagger.viewmodel;

namespace Tagger.services
{
    public class FileDropHandler : IDropTarget
    {
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
            WeakReferenceMessenger.Default.Send(new ExecuteFileDrop(fileIds, tags));
        }
    }
}
