using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using System.IO;
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
    public class TagDropHandler : IDropTarget
    {
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
                dropInfo.Effects= DragDropEffects.Link;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
            }else
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

            foreach (var tag in tagsToApply)
            {
                if (dropInfo.Data is DraggedObjectsPackage<FileItemViewModel>)
                {
                    WeakReferenceMessenger.Default.Send(new ExecuteTagDrop(tag.Id));
                }

                if (dropInfo.Data is DataObject data && data.ContainsFileDropList())
                {
                    var paths = data.GetFileDropList().Cast<string>().ToArray();
                    WeakReferenceMessenger.Default.Send(new ExecuteExternalTagDrop(tag.Id, paths));
                }

                if (dropInfo.Data is string[] filePaths)
                    WeakReferenceMessenger.Default.Send(new ExecuteExternalTagDrop(tag.Id, filePaths));
            }
        }
    }
}
