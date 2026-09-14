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

    public record SelectedItemsChangedMessage(List<FileItemViewModel> selectedItems, int previousCount);

    public record ChangeProgressStatus(bool isLoading, string value);

    public record ApplyTagMessage(List<int> tagIds, List<int> fileIds);
    public record ApplyFileMessage(List<int> tagIds, List<int> fileIds);
    public record RemoveTagMessage(int tagId);

    public record ExecuteTagDrop(TagAssignmentPayload assignmentPayload);
    public record ExecuteFileDrop(List<int> fileIds, DraggedObjectsPackage<TagItemViewModel> uiTags);
    public record ExecuteExternalTagDrop(List<int> tagIds, string[] paths);

    public record RemoveSelectedTag(List<TagItemViewModel> tagsToRemove);

    public record DetachTagMessage(int tagId, HashSet<int> detachedFiles);

    public class AddTagMessage(string tagName) : AsyncRequestMessage<bool>()
    {
        public string TagName { get; } = tagName;
    }

    public class RequestUiTagsDictionaryMessage : RequestMessage<Dictionary<int, TagItemViewModel>> { }

    public class RequestFilteredUITags(string filter) : AsyncRequestMessage<List<string>>
    {
        public string Filter { get; } = filter;
    }

    public enum ScanStatus
    {
        Started,
        Finished,
        CanceledByUser,
        CanceledByFolderChange
    }
}
