using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using Tagger.model;
using Tagger.services.Drag_Drop;

namespace Tagger.messages
{
    public record FolderChangedMessage(string newPath);

    public record ScanStateChangedMessage(ScanStatus status, string folderPath);

    public record FilesScannedBatchMessage(List<FileRecord> FilesBatch);

    public record SearchToSaveMessage(string search);

    public record ApplySavedSearchMessage(SavedSearch search);

    public record ApplyTagToSearch(List<Tag> tags);

    public record SelectedItemsChangedMessage(List<FileRecord> selectedItems);

    public record ChangeProgressStatus(bool isLoading, string value);

    public record ApplyTagMessage(int tagId, List<int> fileIds);
    public record RemoveTagMessage(int tagId);

    public record ApplyFileMessage(int fileId, List<int> tagIds);

    public record ExecuteTagDrop(int tagId);
    public record ExecuteFileDrop(int fileId, DraggedObjectsPackage<Tag> tags);
    public record ExecuteExternalTagDrop(int tagId, string[] paths);

    public record RemoveSelectedTag(List<Tag> tagsToRemove);

    public enum ScanStatus
    {
        Started,
        Finished,
        CanceledByUser,
        CanceledByFolderChange
    }
}
