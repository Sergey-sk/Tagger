using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Tagger.messages;
using Tagger.services.interfaces;
using Tagger.viewmodel.FileViewModels;

namespace Tagger.viewmodel.TagManager
{
    public partial class TagListViewModel : ViewModelBase, IFolderConsumer, ITagCollectionController
    {
        private readonly ITagService _tagService;
        private readonly ITagCacheService _tagCacheService;
        private readonly ITagStateContext _tagStateContext;
        private readonly IDialogService _dialogService;
        private readonly IFileTagService _fileTagService;

        private ObservableCollection<TagItemViewModel> Tags { get => _tagStateContext.Tags; set => _tagStateContext.Tags = value; }

        public string SearchText { get => _tagStateContext.SearchText; set => _tagStateContext.SearchText = value; }

        [ObservableProperty]
        private ICollectionView? _tagsView;

        private List<FileItemViewModel> SelectedFiles { get => _tagStateContext.SelectedFiles; set => _tagStateContext.SelectedFiles = value; }

        public TagListViewModel(ITagService tagService,
                                ITagCacheService tagCacheService,
                                IDialogService dialogService,
                                IFileTagService fileTagService,
                                ITagStateContext tagStateContext)
        {
            _tagService = tagService;
            _tagCacheService = tagCacheService;
            _dialogService = dialogService;
            _fileTagService = fileTagService;
            _tagStateContext = tagStateContext;

            _tagStateContext.PropertyChanged += OnStateContextPropertyChanged;

            RegisterMessages();
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<ApplyFileMessage>(this, async (r, message) =>
                await OnApplyFile(message));

            WeakReferenceMessenger.Default.Register<RequestFilteredUITags>(this, (r, message) =>
            {
                var filteredTags = GetFilteredTags(message.Filter);
                message.Reply(filteredTags);
            });

            WeakReferenceMessenger.Default.Register<DetachTagMessage>(this, (r, message) =>
            {
                using (TagsView?.DeferRefresh())
                {
                    var tag = Tags.FirstOrDefault(t => t.Id == message.tagId);

                    if (tag != null)
                    {
                        tag.DecrementFilesCount(message.detachedFiles.Count);
                        _tagCacheService.RemoveTagForSelection(tag.Id);
                    }
                }

                TagsView?.Refresh();
            });

            WeakReferenceMessenger.Default.Register<DetachAllTagMessage>(this, (r, m) =>
            {
                using (TagsView?.DeferRefresh())
                {
                    foreach (var tag in Tags)
                    {
                        if (m.file.Tags.Any(t => t.Id == tag.Id))
                        {
                            tag.DecrementFilesCount();
                            _tagCacheService.RemoveTagForSelection(tag.Id);
                        }
                    }
                }

                TagsView?.Refresh();
            });

            WeakReferenceMessenger.Default.Register<SelectedItemsChangedMessage>(this, async (r, message) =>
                await OnSelectedFilesChangedMessage(message));
        }

        private void OnStateContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ITagStateContext.SearchText))
            {
                TagsView?.Refresh();
                OnPropertyChanged(nameof(SearchText));
            }
        }

        private async Task OnSelectedFilesChangedMessage(SelectedItemsChangedMessage message)
        {
            SelectedFiles = message.selectedItems;
            ApplyTagToFilesCommand.NotifyCanExecuteChanged();

            foreach (var tag in Tags)
            {
                tag.RefreshSelectionState();
            }

            if (SelectedFiles == null || SelectedFiles.Count == 0)
            {
                _tagCacheService.RemoveAllTagForSelection();
                return;
            }

            var tagsForSelection = Tags.Where(t => t.IsAttachedToAllSelectedFiles || t.IsAttachedToAnySelectedFiles).ToList();

            _tagCacheService.RemoveAllTagForSelection();
            foreach (var tag in tagsForSelection)
                _tagCacheService.AddTagForSelection(tag);
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

            if (obj is TagItemViewModel tag)
            {
                return tag.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }

            return false;
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
                        uiTag.IncrementFilesCount(message.fileIds.Count);

                        _tagCacheService.AddTagForSelection(uiTag);
                    }
                }
            }
            TagsView?.Refresh();
        }

        [RelayCommand]
        private async Task LoadTagsAsync()
        {
            try
            {
                _tagStateContext.SelectedTags.Clear();
                var tags = await _tagService.LoadTagsAsync();
                var viewModelTags = tags.Select(t => new TagItemViewModel(t, () => SelectedFiles));
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

                var newFileIds = await _fileTagService.LinkFilesToTagAsync([.. assignmentPayload.TagIds], [.. selectedFileIds]);

                AppendFilesToUiTags(assignmentPayload.TagIds, newFileIds.Count);

                WeakReferenceMessenger.Default.Send(new ApplyTagMessage(assignmentPayload.TagIds, selectedFileIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AppendFilesToUiTags(List<int> tagIds, int filesCount)
        {
            if (filesCount <= 0) return;

            using (TagsView?.DeferRefresh())
            {
                var tagsInUi = Tags.Where(t => tagIds.Contains(t.Id)).ToList();
                if (tagsInUi == null || tagsInUi.Count == 0) return;

                foreach (var uiTag in tagsInUi)
                {
                    uiTag.IncrementFilesCount(filesCount);

                    _tagCacheService.AddTagForSelection(uiTag);
                }
            }

            TagsView?.Refresh();
        }

        private bool CanApplyFilesToTag() => SelectedFiles.Count > 0;

        public ValueTask OnFolderChanged(string path)
        {
            SearchText = string.Empty;
            SelectedFiles.Clear();
            return ValueTask.CompletedTask;
        }
    }
}
