using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Windows;
using Tagger.dto;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;
using Tagger.viewmodel.TagManagerViewModel;

namespace Tagger.viewmodel.InfoPanelViewModel
{
    public partial class InfoPanelViewModel : ObservableObject
    {
        private readonly IInfoPanelService _infoPanelService;
        private readonly IDialogService _dialogService;
        [ObservableProperty]
        private ObservableCollection<TagItemViewModel> _sharedTagsForSelection;

        [ObservableProperty]
        private string _searchTagNameText;

        [ObservableProperty]
        private bool _isSuggestionsOpen;

        [ObservableProperty]
        private bool _isPanelOpen = false;

        [ObservableProperty]
        private ObservableCollection<string> _filteredTags;

        [ObservableProperty]
        private string _selectedTag;

        private bool _isSelectingFromPopup = false;

        public InfoPanelViewModel(IInfoPanelService infoPanelService, IDialogService dialogService)
        {
            _infoPanelService = infoPanelService;
            _dialogService = dialogService;

            WeakReferenceMessenger.Default.Register<SelectedItemsChangedMessage>(this, (r, message) =>
            {
                if (message.selectedItems.Count > 0 && message.previousCount == 0) IsPanelOpen = true;
                else if (message.selectedItems.Count == 0) IsPanelOpen = false;
            });
        }

        partial void OnSearchTagNameTextChanged(string? oldValue, string newValue)
        {
            if (_isSelectingFromPopup) return;
            SearchCommand.Execute(newValue);
        }

        [RelayCommand(AllowConcurrentExecutions =false)]
        private async Task SearchAsync(string newValue, CancellationToken token)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                IsSuggestionsOpen = false;
                FilteredTags?.Clear();
                return;
            }

            try
            {
                await Task.Delay(300, token);

                var reqMessage = new RequestFilteredUITags(newValue);
                await WeakReferenceMessenger.Default.Send(reqMessage);
                if (!reqMessage.HasReceivedResponse)
                {
                    IsSuggestionsOpen = false;
                    FilteredTags?.Clear();
                    return;
                }

                var tags = await reqMessage.Response;

                if (tags.Count == 0)
                {
                    IsSuggestionsOpen = false;
                    return;
                }

                if (FilteredTags == null)
                    FilteredTags = new ObservableCollection<string>(tags);
                else
                {
                    FilteredTags.Clear();
                    foreach (var tag in tags)
                        FilteredTags.Add(tag);
                }

                IsSuggestionsOpen = true;
            }
            catch (OperationCanceledException)
            {

            }
        }

        partial void OnSelectedTagChanged(string? oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(newValue)) return;

            _isSelectingFromPopup = true;
            try
            {
                SearchTagNameText = newValue;
                IsSuggestionsOpen = false;
            }
            finally
            {
                _isSelectingFromPopup = false;
                SelectedTag = null;
            }
        }

        [RelayCommand]
        private async Task DetachTagFromFileAsync(TagAssignmentPayload assignmentPayload)
        {
            var tagId = assignmentPayload.TagIds[0];
            var attachedFiles = assignmentPayload.Files.Where(f => f.Tags.Any(t => t.Id == tagId)).Select(f => f.Id).ToHashSet();
            var result = _dialogService.ShowMessage($"Открепить тег от всех выбранных файлов? ({attachedFiles.Count} шт.)",
                                                     "Открепление тега",
                                                     MessageBoxButton.YesNo,
                                                     MessageBoxImage.Question);

            if (result == MessageBoxResult.No) return;
            await _infoPanelService.DetachTagFromFilesAsync(tagId, assignmentPayload.Files.Select(f => f.Id).ToList());

            WeakReferenceMessenger.Default.Send(new DetachTagMessage(tagId, attachedFiles));
        }

        [RelayCommand]
        private void CloseOpenPanel() => IsPanelOpen = !IsPanelOpen;

        [RelayCommand]
        private async Task AttachTagToFilesAsync(ObservableCollection<FileItemViewModel> selectedFiles)
        {
            if (string.IsNullOrWhiteSpace(SearchTagNameText)) return;

            int tagId = await _infoPanelService.AttachTagToFilesAsync(SearchTagNameText, selectedFiles.ToList());

            if (tagId == -1)
            {
                SearchTagNameText = string.Empty;
                return;
            }

            WeakReferenceMessenger.Default.Send(new ApplyFileMessage([tagId], selectedFiles.Select(f => f.Id).ToList()));
            WeakReferenceMessenger.Default.Send(new ApplyTagMessage([tagId], selectedFiles.Select(f => f.Id).ToList()));

            SearchTagNameText = string.Empty;
        }
    }
}
