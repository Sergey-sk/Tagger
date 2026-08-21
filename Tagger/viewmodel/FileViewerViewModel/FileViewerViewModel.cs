using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using Tagger.messages;
using Tagger.model;
using Tagger.services;
using Tagger.services.Drag_Drop;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.FileViewerViewModel
{
    public partial class FileViewerViewModel : ObservableObject,
        IRecipient<FolderChangedMessage>,
        IRecipient<FilesScannedBatchMessage>
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IDialogService _dialogService;
        private readonly IFileService _fileService;

        public IDragSource DragHandler { get; }
        public IDropTarget DropHandler { get; }

        private string _currentActivePath;
        private CancellationTokenSource? _folderCts;
        private CancellationTokenSource? _searchCts;
        private bool _isLoading;
        private List<FileRecord> _cachedFiles = [];
        private List<Tag> _selectedTags = [];

        [ObservableProperty]
        private ObservableCollection<FileRecord> _files;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSearching))]
        [NotifyCanExecuteChangedFor(nameof(TriggerSearchWithDebounceCommand))]
        private string _currentSearchText;

        public ObservableCollection<FileRecord> SelectedFiles { get; set; } = [];

        public bool IsSearching => !string.IsNullOrWhiteSpace(CurrentSearchText) || _selectedTags.Count > 0;

        public FileViewerViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, IDialogService dialogService, IFileService fileService)
        {
            _contextFactory = contextFactory;
            _dialogService = dialogService;
            _fileService = fileService;

            DragHandler = new FileDragHandler(dialogService);
            DropHandler = new FileDropHandler();

            _currentActivePath = Properties.Settings.Default.FolderPath;

            SelectedFiles.CollectionChanged += (s, e) =>
            {
                var currentSelection = SelectedFiles.ToList();
                WeakReferenceMessenger.Default.Send(new SelectedItemsChangedMessage(currentSelection));
            };

            WeakReferenceMessenger.Default.Register<FolderChangedMessage>(this);
            WeakReferenceMessenger.Default.Register<FilesScannedBatchMessage>(this);

            WeakReferenceMessenger.Default.Register<ApplySavedSearchMessage>(this, async (r, message) =>
            {
                CurrentSearchText = message.search?.QueryText ?? string.Empty;
            });

            WeakReferenceMessenger.Default.Register<ScanStateChangedMessage>(this, async (r, message) =>
            {
                await OnScanStateChanged(message);
            });

            WeakReferenceMessenger.Default.Register<ApplyTagToSearch>(this, async (r, message) =>
            {
                _selectedTags = message.tags;
                OnPropertyChanged(nameof(IsSearching));
                await TriggerSearchWithDebounceAsync();
            });

            WeakReferenceMessenger.Default.Register<ApplyTagMessage>(this, async (r, message) =>
                await OnApplyTag(message));

            WeakReferenceMessenger.Default.Register<RemoveTagMessage>(this, async (r, message) =>
                await OnRemoveTag(message));

            WeakReferenceMessenger.Default.Register<ExecuteFileDrop>(this, async (r, message) =>
                await ApplyTagsToFilesAsync(message.fileId, message.tags));
        }

        private async Task OnScanStateChanged(ScanStateChangedMessage message)
        {
            try
            {
                switch (message.status)
                {
                    case ScanStatus.Started:
                    case ScanStatus.CanceledByFolderChange:
                        Files?.Clear();
                        _cachedFiles.Clear();
                        _isLoading = true;
                        break;

                    case ScanStatus.CanceledByUser:
                        _isLoading = false;
                        break;

                    case ScanStatus.Finished:
                        _isLoading = false;

                        if (IsSearching) return;
                        _folderCts?.Cancel();
                        _folderCts = new CancellationTokenSource();
                        var token = _folderCts.Token;

                        await LoadFilesAsync(message.folderPath, token);
                        break;

                    default:
                        break;
                }
                ;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка при сканировании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Вызывается при перетаскивании файла на тег или применении файла к тегу
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task OnApplyTag(ApplyTagMessage message)
        {
            var savedTag = await _fileService.ApplyFilesToTagsAsync(message.tagId, message.fileIds);
            if (savedTag == null) return;

            var fileIdsSet = message.fileIds.ToHashSet();

            List<FileRecord> uiFiles = _cachedFiles
                .Where(f => fileIdsSet.Contains(f.Id))
                .ToList();

            foreach (var uiFile in uiFiles)
            {
                if (!uiFile.Tags.Any(t => t.Id == message.tagId))
                    uiFile.Tags.Add(savedTag);
            }

            await TriggerSearchWithDebounceAsync();
        }

        private async Task OnRemoveTag(RemoveTagMessage message)
        {
            foreach (var file in _cachedFiles)
                file.Tags.RemoveAll(t => t.Id == message.tagId);

            _selectedTags.RemoveAll(t => t.Id == message.tagId);
            OnPropertyChanged(nameof(IsSearching));

            await TriggerSearchWithDebounceAsync();
        }

        partial void OnCurrentSearchTextChanged(string value)
        {
            _ = TriggerSearchWithDebounceAsync();
        }

        private async Task LoadFilesAsync(string path, CancellationToken token)
        {
            if (string.IsNullOrEmpty(path) || path == "Не выбрана")
            {
                Files?.Clear();
                _cachedFiles.Clear();
                return;
            }

            _isLoading = true;
            WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(true, "Поиск и загрузка файлов..."));

            try
            {
                var initialFiles = await _fileService.LoadFirstBatchFilesAsync(path, token);

                if (Files == null)
                    Files = new ObservableCollection<FileRecord>(initialFiles);
                else
                {
                    Files.Clear();

                    foreach (var file in initialFiles)
                        Files.Add(file);
                }

                try
                {
                    var allFiles = await Task.Run(() => _fileService.LoadFilesForPathInBackgroundAsync(path, token), token);

                    _cachedFiles.Clear();
                    _cachedFiles.AddRange(allFiles);

                    if (!string.IsNullOrEmpty(CurrentSearchText) || _selectedTags.Count > 0)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => _ = TriggerSearchWithDebounceAsync());
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Ошибка фонового потока: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (Files.Count == 0)
                {
                    WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(false, "Файлы не найдены. Просканируйте папку."));
                    return;
                }

                WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(false, $"Показаны первые {Files.Count} файлов. Уточните поиск."));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    _isLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadOnStart()
        {
            _folderCts?.Cancel();
            _folderCts = new CancellationTokenSource();
            var token = _folderCts.Token;

            await LoadFilesAsync(_currentActivePath, token);
        }

        [RelayCommand]
        private async Task TriggerSearchWithDebounceAsync()
        {
            if (_isLoading && _cachedFiles.Count == 0) return;

            try
            {
                List<int> tagIds = [.. _selectedTags.Select(t => t.Id)];

                var resultFiles = await _fileService.SearchWithDebounceAsync(CurrentSearchText, _cachedFiles, _currentActivePath, tagIds, _searchCts);

                if (Files == null)
                    Files = new ObservableCollection<FileRecord>(resultFiles);
                else
                {
                    Files.Clear();

                    foreach (var file in resultFiles)
                        Files.Add(file);
                }

                if (!string.IsNullOrWhiteSpace(CurrentSearchText))
                {
                    WeakReferenceMessenger.Default.Send(new SearchToSaveMessage(CurrentSearchText));
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Вызывается при перетаскивании тега на файл
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        private async Task ApplyTagsToFilesAsync(int fileId, DraggedObjectsPackage<Tag> tags)
        {
            try
            {
                var tagIds = tags.Objects.Select(t => t.Id).ToList();

                await _fileService.ApplyTagsToFilesAsync(fileId, tagIds);

                await UpdateUiFiles(fileId, tags.Objects);

                WeakReferenceMessenger.Default.Send(new ApplyFileMessage(fileId, tagIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateUiFiles(int fileId, List<Tag> tagsToAdd)
        {
            var fileInUi = Files.FirstOrDefault(f => f.Id == fileId);
            if (fileInUi != null)
            {
                foreach (var tag in tagsToAdd)
                {
                    if (!fileInUi.Tags.Any(f => f.Id == tag.Id))
                        fileInUi.Tags.Add(tag);
                }
            }

            await TriggerSearchWithDebounceAsync();
        }

        public async void Receive(FolderChangedMessage message)
        {
            try
            {
                _currentActivePath = message.newPath;

                _folderCts?.Cancel();
                _folderCts = new CancellationTokenSource();
                var token = _folderCts.Token;

                _selectedTags.Clear();
                SelectedFiles.Clear();
                _cachedFiles.Clear();

                if (CurrentSearchText != null) CurrentSearchText = string.Empty;
                await LoadFilesAsync(message.newPath, token);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка при смене папки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Receive(FilesScannedBatchMessage message)
        {
            var newFiles = message.FilesBatch;

            if (newFiles.Count == 0)
            {
                Files?.Clear();
                _cachedFiles.Clear();
                return;
            }

            _cachedFiles.AddRange(newFiles);

            if (IsSearching)
            {
                _ = TriggerSearchWithDebounceAsync();
            }
            else
            {
                foreach (var file in newFiles)
                    if (Files?.Count < 3000) Files.Add(file);
            }
        }
    }
}