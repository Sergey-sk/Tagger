using CommunityToolkit.Mvvm.Messaging.Messages;
using Tagger.model;
using Tagger.services.Drag_Drop;
using Tagger.viewmodel;
using Tagger.viewmodel.FileViewModels;
using Tagger.viewmodel.TagManagerViewModel;

namespace Tagger.messages
{
    public record FolderChangedMessage(string newPath);

    public record ScanStateChangedMessage(ScanStatus status, string folderPath);

    public record FilesScannedBatchMessage(int filesCount);

    public record SearchToSaveMessage(string search);

    public record ApplySavedSearchMessage(SavedSearch search);

    public record ApplyTagToSearch(HashSet<int> tagIds);

    public record SelectedItemsChangedMessage(List<FileItemViewModel> selectedItems, int previousCount);

    public record ChangeProgressStatus(bool isLoading, string value);

    public record ApplyTagMessage(List<int> tagIds, List<int> fileIds);
    public record ApplyFileMessage(List<int> tagIds, List<int> fileIds);
    public record RemoveTagMessage(HashSet<int> tagIds);

    public record ExecuteTagDrop(TagAssignmentPayload assignmentPayload);
    public record ExecuteFileDrop(List<int> fileIds, DraggedObjectsPackage<TagItemViewModel> uiTags);
    public record ExecuteExternalTagDrop(List<int> tagIds, string[] paths);

    public record RemoveSelectedTag(List<TagItemViewModel> tagsToRemove);

    public record DetachTagMessage(int tagId, HashSet<int> detachedFiles);

    public record DetachAllTagMessage(FileItemViewModel file);

    public record SearchResultAvailableMessage(List<FileItemViewModel> foundFiles);

    public class AddTagMessage(string tagName) : AsyncRequestMessage<bool>()
    {
        public string TagName { get; } = tagName;
    }

    public class RequestUiTagsDictionaryMessage : RequestMessage<Dictionary<int, TagItemViewModel>> { }

    public class RequestFilteredUITags(string filter) : AsyncRequestMessage<List<string>>
    {
        public string Filter { get; } = filter;
    }

    public class RequestCurrentSearchQueryMessage : RequestMessage<bool> { }

    public enum ScanStatus
    {
        Started,
        Finished,
        CanceledByUser,
        CanceledByFolderChange
    }
}
