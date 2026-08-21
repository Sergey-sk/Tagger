using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
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

        public IDragSource DragHandler { get; }
        public IDropTarget DropHandler { get; }

        [ObservableProperty]
        private ObservableCollection<Tag> _tags;

        [ObservableProperty]
        private string _searchText;

        [ObservableProperty]
        private ICollectionView? _tagsView;

        public ObservableCollection<Tag> SelectedTags { get; set; } = [];

        private List<FileRecord> _selectedFiles = [];

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
                                   IFileIndexingService fileIndexingService)
        {
            _dialogService = dialogService;
            _tagService = tagService;
            _fileIndexingService = fileIndexingService;

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

            WeakReferenceMessenger.Default.Register<SelectedItemsChangedMessage>(this, (r, message) =>
            {
                _selectedFiles = message.selectedItems;
                ApplyTagToFilesCommand.NotifyCanExecuteChanged();
            });

            WeakReferenceMessenger.Default.Register<ExecuteTagDrop>(this, async (r, message) =>
                await ApplyTagToFilesAsync(message.tagId));

            WeakReferenceMessenger.Default.Register<ExecuteExternalTagDrop>(this, async (r, message) =>
                await HandleExternalDropAsync(message.tagId, message.paths));

            WeakReferenceMessenger.Default.Register<ApplyFileMessage>(this, async (r, message) =>
                await OnApplyFile(message));

            WeakReferenceMessenger.Default.Register<RemoveSelectedTag>(this, (r, message) =>
            {
                foreach(var tag in message.tagsToRemove)
                {
                    SelectedTags.Remove(tag);
                }
            });
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task OnApplyFile(ApplyFileMessage message)
        {
            var savedFile = await _tagService.ApplyTagsToFilesAsync(message.fileId, message.tagIds);
            if (savedFile == null) return;

            var tagIdsSet = message.tagIds.ToHashSet();

            List<Tag> uiTags = Tags
                .Where(t => tagIdsSet.Contains(t.Id))
                .ToList();

            foreach (var uiTag in uiTags)
            {
                if (!uiTag.Files.Any(f => f.Id == message.fileId))
                    uiTag.Files.Add(savedFile);
            }

            var sorted = Tags.OrderByDescending(t => t.Files.Count).ToList();
            Tags.Clear();
            foreach (var tag in sorted)
                Tags.Add(tag);

            TagsView?.Refresh();
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
                Tags = new ObservableCollection<Tag>(tags);

                TagsView = CollectionViewSource.GetDefaultView(Tags);
                TagsView.Filter = FilterTags;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanCreateTag))]
        private async Task CreateTagAsync()
        {
            try
            {
                var newTag = await _tagService.CreateTagAsync(SearchText.Trim());

                Tags.Add(newTag);

                if (!SelectedTags.Contains(newTag))
                {
                    SelectedTags.Add(newTag);
                    OnPropertyChanged(nameof(CurrentFilterStr));
                }

                SearchText = string.Empty;
            }
            catch (ArgumentException ex)
            {
                SearchText = string.Empty;
                _dialogService.ShowMessage(ex.Message, "Внимание!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
        private async Task ApplyTagToFilesAsync(int tagId)
        {
            try
            {
                var selectedFileIds = _selectedFiles.Select(f => f.Id).ToList();

                await _tagService.ApplyFilesToTagsAsync(tagId, selectedFileIds);

                UpdateUiTags(tagId, _selectedFiles);

                WeakReferenceMessenger.Default.Send(new ApplyTagMessage(tagId, selectedFileIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUiTags(int tagId, List<FileRecord> filesToAdd)
        {
            var tagInUi = Tags.FirstOrDefault(t => t.Id == tagId);
            if (tagInUi != null)
            {
                foreach (var file in filesToAdd)
                {
                    if (!tagInUi.Files.Any(f => f.Id == file.Id))
                        tagInUi.Files.Add(file);
                }
            }

            var sorted = Tags.OrderByDescending(t => t.Files.Count).ToList();
            Tags.Clear();
            foreach (var tag in sorted)
                Tags.Add(tag);

            TagsView?.Refresh();
        }

        private bool CanApplyFilesToTag() => _selectedFiles.Count > 0;

        private async Task HandleExternalDropAsync(int tagId, string[] filePaths)
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

                await _tagService.ApplyFilesToTagsAsync(tagId, fileIds);

                var files = await _tagService.GetFilesByIdsAsync(fileIds);

                UpdateUiTags(tagId, files);

                isOperationActive = false;

                WeakReferenceMessenger.Default.Send(new ApplyTagMessage(tagId, fileIds));
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
