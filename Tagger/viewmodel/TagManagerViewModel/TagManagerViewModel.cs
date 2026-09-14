using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Tagger.dto;
using Tagger.messages;
using Tagger.model;
using Tagger.services;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.TagManagerViewModel
{
    public partial class TagManagerViewModel : ObservableObject
    {
        private readonly IDialogService _dialogService;
        private readonly ITagService _tagService;
        private readonly IFileIndexingService _fileIndexingService;
        private readonly IFileTagService _fileTagService;

        public IDragSource DragHandler { get; }
        public IDropTarget DropHandler { get; }

        [ObservableProperty]
        private ObservableCollection<TagItemViewModel> _tags;

        [ObservableProperty]
        private string _searchText;

        [ObservableProperty]
        private ICollectionView? _tagsView;

        [ObservableProperty]
        private ObservableCollection<TagItemViewModel> _sharedTagsForSelection;

        public ObservableCollection<TagItemViewModel> SelectedTags { get; set; } = [];

        private List<FileItemViewModel> _selectedFiles = [];

        public string CurrentFilterStr
        {
            get
            {
                return SelectedTags.Count > 0
                    ? string.Join(", ", SelectedTags.Select(t => t.Name))
                    : "Ничего не выбрано";
            }
        }

        public TagManagerViewModel(IDialogService dialogService,
                                   ITagService tagService,
                                   IFileIndexingService fileIndexingService,
                                   IFileTagService fileTagService)
        {
            _dialogService = dialogService;
            _tagService = tagService;
            _fileIndexingService = fileIndexingService;
            _fileTagService = fileTagService;

            DragHandler = new TagDragHandler(dialogService);
            DropHandler = new TagDropHandler();

            SelectedTags.CollectionChanged += (s, e) =>
            {
                WeakReferenceMessenger.Default.Send(new ApplyTagToSearch(SelectedTags.ToList()));
                OnPropertyChanged(nameof(CurrentFilterStr));
            };

            WeakReferenceMessenger.Default.Register<FolderChangedMessage>(this, (r, message) =>
            {
                ResetSelectedTags();
                _selectedFiles.Clear();
                SearchText = string.Empty;
            });

            WeakReferenceMessenger.Default.Register<SelectedItemsChangedMessage>(this, async (r, message) =>
                await OnSelectedFilesChangedMessage(message));

            WeakReferenceMessenger.Default.Register<ExecuteTagDrop>(this, async (r, message) =>
                await ApplyTagToFilesAsync(message.assignmentPayload));

            WeakReferenceMessenger.Default.Register<ExecuteExternalTagDrop>(this, async (r, message) =>
                await HandleExternalDropAsync(message.tagIds, message.paths));

            WeakReferenceMessenger.Default.Register<ApplyFileMessage>(this, async (r, message) =>
                await OnApplyFile(message));

            WeakReferenceMessenger.Default.Register<RemoveSelectedTag>(this, (r, message) =>
            {
                foreach (var tag in message.tagsToRemove)
                {
                    SelectedTags.Remove(tag);
                }
            });

            WeakReferenceMessenger.Default.Register<RequestUiTagsDictionaryMessage>(this, (r, message) =>
                OnRequestUiTagsDictionary(message));

            WeakReferenceMessenger.Default.Register<DetachTagMessage>(this, (r, message) =>
            {
                using (TagsView?.DeferRefresh())
                {
                    var tag = Tags.FirstOrDefault(t => t.Id == message.tagId);

                    if (tag != null)
                    {
                        tag.DecrementFilesCount(message.detachedFiles.Count);
                        SharedTagsForSelection.Remove(tag);
                    }
                }

                TagsView?.Refresh();
            });

            WeakReferenceMessenger.Default.Register<AddTagMessage>(this, (r, message) =>
            {
                message.Reply(CreateTagAsync(message.TagName));
            });

            WeakReferenceMessenger.Default.Register<RequestFilteredUITags>(this, (r, message) =>
            {
                var filteredTags = GetFilteredTags(message.Filter);
                message.Reply(filteredTags);
            });
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task OnApplyFile(ApplyFileMessage message)
        {
            if (message.tagIds == null || message.tagIds.Count == 0) return;

            var addedTagIdsSet = message.tagIds.ToHashSet();

            using (TagsView?.DeferRefresh())
            {
                foreach (var uiTag in Tags)
                {
                    if (addedTagIdsSet.Contains(uiTag.Id))
                    {
                        if (addedTagIdsSet.Contains(uiTag.Id))
                            uiTag.IncrementFilesCount(message.fileIds.Count);

                        if (SharedTagsForSelection == null)
                            SharedTagsForSelection = new ObservableCollection<TagItemViewModel>();

                        if (!SharedTagsForSelection.Any(t => t.Id == uiTag.Id))
                            SharedTagsForSelection.Add(uiTag);
                    }
                }
            }
            TagsView?.Refresh();
        }

        private async Task OnSelectedFilesChangedMessage(SelectedItemsChangedMessage message)
        {
            _selectedFiles = message.selectedItems;
            ApplyTagToFilesCommand.NotifyCanExecuteChanged();

            foreach (var tag in Tags)
            {
                tag.RefreshSelectionState();
            }

            if (_selectedFiles == null || _selectedFiles.Count == 0)
            {
                SharedTagsForSelection?.Clear();
                return;
            }

            var tagsForSelection = Tags.Where(t => t.IsAttachedToAllSelectedFiles || t.IsAttachedToAnySelectedFiles).ToList();
            if (SharedTagsForSelection == null)
                SharedTagsForSelection = new ObservableCollection<TagItemViewModel>(tagsForSelection);
            else
            {
                SharedTagsForSelection.Clear();
                foreach (var tag in tagsForSelection)
                    SharedTagsForSelection.Add(tag);
            }
        }

        private void OnRequestUiTagsDictionary(RequestUiTagsDictionaryMessage message)
        {
            var tagsDictionary = Tags.ToDictionary(t => t.Id);
            message.Reply(tagsDictionary);
        }

        private void ResetSelectedTags()
        {
            if (Tags == null || !Tags.Any()) return;

            SelectedTags.Clear();

            OnPropertyChanged(nameof(CurrentFilterStr));
        }

        partial void OnSearchTextChanged(string value)
        {
            TagsView?.Refresh();
            CreateTagCommand.NotifyCanExecuteChanged();
        }

        private List<string> GetFilteredTags(string filter)
        {
            return Tags.Where(t => t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                       .Select(t => t.Name)
                       .ToList();
        }

        private bool FilterTags(object obj)
        {
            if (string.IsNullOrEmpty(SearchText)) return true;

            if (obj is Tag tag)
            {
                return tag.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        [RelayCommand]
        private async Task LoadTagsAsync()
        {
            try
            {
                SelectedTags.Clear();
                var tags = await _tagService.LoadTagsAsync();
                var viewModelTags = tags.Select(t => new TagItemViewModel(t, () => _selectedFiles.ToList()));
                Tags = new ObservableCollection<TagItemViewModel>(viewModelTags);

                TagsView = CollectionViewSource.GetDefaultView(Tags);
                TagsView.Filter = FilterTags;

                TagsView.SortDescriptions.Clear();
                TagsView.SortDescriptions.Add(new SortDescription(nameof(TagItemViewModel.FilesCount), ListSortDirection.Descending));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanCreateTag))]
        private async Task<bool> CreateTagAsync(string tagName)
        {
            try
            {
                var newTag = await _tagService.CreateTagAsync(tagName);
                var viewModelTag = new TagItemViewModel(newTag, () => _selectedFiles.ToList());

                Tags.Add(viewModelTag);

                //if (!SelectedTags.Contains(viewModelTag))
                //{
                //    SelectedTags.Add(viewModelTag);
                //    OnPropertyChanged(nameof(CurrentFilterStr));
                //}

                SearchText = string.Empty;
                return true;
            }
            catch (ArgumentException ex)
            {
                SearchText = string.Empty;
                _dialogService.ShowMessage(ex.Message, "Внимание!", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool CanCreateTag() => !string.IsNullOrWhiteSpace(SearchText);

        [RelayCommand]
        private async Task RemoveTagAsync(int tagId)
        {
            int filesCount = await _tagService.GetFilesCountByTagId(tagId);

            var result = _dialogService.ShowMessage($"Удалить тег? ({filesCount} файлов)", "Удаление тега", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.No) return;

            try
            {
                await _tagService.RemoveTagAsync(tagId);

                var tagInUI = Tags.FirstOrDefault(t => t.Id == tagId);
                if (tagInUI != null)
                {
                    Tags.Remove(tagInUI);
                    if (SelectedTags.Contains(tagInUI))
                        SelectedTags.Remove(tagInUI);
                }

                WeakReferenceMessenger.Default.Send(new RemoveTagMessage(tagId));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Вызывается, когда файлы перетаскиваются на тег или когда файлы выбираются и добавляются к тегу с помощью ContextMenu
        /// </summary>
        /// <param name="tagId"></param>
        /// <returns></returns>

        [RelayCommand(CanExecute = nameof(CanApplyFilesToTag))]
        private async Task ApplyTagToFilesAsync(TagAssignmentPayload assignmentPayload)
        {
            try
            {
                var selectedFileIds = assignmentPayload.Files.Select(f => f.Id).ToList();

                var newFileIds = await _fileTagService.LinkFilesToTagAsync(assignmentPayload.TagIds, selectedFileIds);

                UpdateUiTags(assignmentPayload.TagIds, newFileIds.Count);

                WeakReferenceMessenger.Default.Send(new ApplyTagMessage(assignmentPayload.TagIds, selectedFileIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUiTags(List<int> tagIds, int filesCount)
        {
            if (filesCount <= 0) return;

            using (TagsView?.DeferRefresh())
            {
                var tagsInUi = Tags.Where(t => tagIds.Contains(t.Id)).ToList();
                if (tagsInUi == null || tagsInUi.Count == 0) return;

                foreach (var uiTag in tagsInUi)
                {
                    uiTag.IncrementFilesCount(filesCount);

                    if (SharedTagsForSelection == null)
                        SharedTagsForSelection = new ObservableCollection<TagItemViewModel>();

                    if (!SharedTagsForSelection.Any(t => t.Id == uiTag.Id))
                        SharedTagsForSelection.Add(uiTag);
                }
            }

            TagsView?.Refresh();
        }

        private bool CanApplyFilesToTag() => _selectedFiles.Count > 0;

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

                var fileIds = await _fileIndexingService.AddFilesFromPathAsync(filePaths.ToList(), progress);

                var newFileIds = await _fileTagService.LinkFilesToTagAsync(tagIds, fileIds);

                UpdateUiTags(tagIds, newFileIds.Count);

                isOperationActive = false;

                WeakReferenceMessenger.Default.Send(new ApplyTagMessage(tagIds, fileIds));
                WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(false, "Готово"));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(false, "Ошибка"));
            }
        }
    }
}
