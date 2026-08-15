using System;
using System.Collections.Generic;
using System.Text;
using Tagger.model;

namespace Tagger.messages
{
    public record FolderChangedMessage(string newPath);

    public record ScanStateChangedMessage(ScanStatus status, string folderPath);

    public record FilesScannedBatchMessage(List<FileRecord> FilesBatch);

    public record SearchToSaveMessage(string search);

    public record ApplySavedSearchMessage(SavedSearch search);

    public record ApplyTagToSearch(List<string> tags);

    public record SelectedItemsChangedMessage(List<FileRecord> selectedItems);

    public record ChangeProgressStatus(bool isLoading, string value);

    public record ApplyTagMessage(int tagId, List<int> fileIds);
    public record RemoveTagMessage(string tagName);

    public enum ScanStatus
    {
        Started,
        Finished,
        CanceledByUser,
        CanceledByFolderChange
    }
}
