using CommunityToolkit.Mvvm.Messaging.Messages;
using Tagger.dto;
using Tagger.model;
using Tagger.services.Drag_Drop;
using Tagger.viewmodel;
using Tagger.viewmodel.TagManagerViewModel;

namespace Tagger.messages
{
    public record FolderChangedMessage(string newPath);

    public record ScanStateChangedMessage(ScanStatus status, string folderPath);

    public record FilesScannedBatchMessage(List<FileRecord> FilesBatch);

    public record SearchToSaveMessage(string search);

    public record ApplySavedSearchMessage(SavedSearch search);

    public record ApplyTagToSearch(List<TagItemViewModel> tags);

    public record SelectedItemsChangedMessage(List<FileItemViewModel> selectedItems);

    public record ChangeProgressStatus(bool isLoading, string value);

    public record ApplyTagMessage(List<int> tagIds, List<int> fileIds);
    public record ApplyFileMessage(List<int> tagIds, List<int> fileIds);
    public record RemoveTagMessage(int tagId);

    public record ExecuteTagDrop(TagAssignmentPayload assignmentPayload);
    public record ExecuteFileDrop(List<int> fileIds, DraggedObjectsPackage<TagItemViewModel> uiTags);
    public record ExecuteExternalTagDrop(List<int> tagIds, string[] paths);

    public record RemoveSelectedTag(List<TagItemViewModel> tagsToRemove);

    public record DetachTagMessage(int tagId, int detachedFilesCount);

    public class RequestUiTagsDictionaryMessage : RequestMessage<Dictionary<int, TagItemViewModel>> { }

    public enum ScanStatus
    {
        Started,
        Finished,
        CanceledByUser,
        CanceledByFolderChange
    }
}
