using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

//все о файлах - поиск открытие, любое взаимодействие, d&d на тег
namespace Tagger.viewmodel.FileViewerViewModel
{
    public partial class FileViewerViewModel : ObservableObject,
        IRecipient<FolderChangedMessage>,
        IRecipient<FilesScannedBatchMessage>
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IDialogService _dialogService;

        private string _currentActivePath;
        private CancellationTokenSource? _folderCts;
        private CancellationTokenSource? _searchCts;
        private bool _isLoading;
        private List<FileRecord> _cachedFiles = [];

        [ObservableProperty]
        private ObservableCollection<FileRecord> _files;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(TriggerSearchWithDebounceCommand))]
        private string _currentSearchText;

        public ObservableCollection<FileRecord> SelectedFiles { get; set; } = [];

        private List<string> _selectedTagNames = new();

        public FileViewerViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, IDialogService dialogService)
        {
            _contextFactory = contextFactory;
            _dialogService = dialogService;

            WeakReferenceMessenger.Default.Register<FolderChangedMessage>(this);
            WeakReferenceMessenger.Default.Register<FilesScannedBatchMessage>(this);

            _currentActivePath = Properties.Settings.Default.FolderPath;

            SelectedFiles.CollectionChanged += (s, e) =>
            {
                var currentSelection = SelectedFiles.ToList();
                WeakReferenceMessenger.Default.Send(new SelectedItemsChangedMessage(currentSelection));
            };

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
                _selectedTagNames = message.tags;
                await TriggerSearchWithDebounceAsync();
            });

            WeakReferenceMessenger.Default.Register<ApplyTagMessage>(this, async (r, message) =>
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var tag = await context.Tags
                    .FirstOrDefaultAsync(t => t.Id == message.tagId);

                if (tag == null) return;

                List<FileRecord> taggedFiles = _cachedFiles
                    .Where(f => message.fileIds.Contains(f.Id)).ToList();

                foreach(var file in taggedFiles)
                {
                    file.Tags.Add(tag);
                }

                await TriggerSearchWithDebounceAsync();
            });

            WeakReferenceMessenger.Default.Register<RemoveTagMessage>(this, async (r, message) =>
            {
                foreach (var file in _cachedFiles)
                    file.Tags.RemoveAll(t => t.Name == message.tagName);

                _selectedTagNames.Remove(message.tagName);

                await TriggerSearchWithDebounceAsync();
            });
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
                        break;

                    case ScanStatus.CanceledByUser:
                        break;

                    case ScanStatus.Finished:
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

        [RelayCommand]
        private async Task LoadOnStart()
        {
            _folderCts?.Cancel();
            _folderCts = new CancellationTokenSource();
            var token = _folderCts.Token;

            await LoadFilesAsync(_currentActivePath, token);
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
                using (var context = await _contextFactory.CreateDbContextAsync(token))
                {

                    var initialFiles = await context.Files
                        .AsNoTracking()
                        .Include(f => f.Tags)
                        .Where(f => f.Path.StartsWith(path))
                        .Take(3000)
                        .ToListAsync(token);

                    token.ThrowIfCancellationRequested();

                    if (Files == null)
                        Files = new ObservableCollection<FileRecord>(initialFiles);
                    else
                    {
                        Files.Clear();

                        foreach (var file in initialFiles)
                            Files.Add(file);
                    }
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var bgContext = await _contextFactory.CreateDbContextAsync();

                        var allFiles = await bgContext.Files
                        .AsNoTracking()
                        .Include(f => f.Tags)
                        .Where(f => f.Path.StartsWith(path))
                        .ToListAsync(token);

                        _cachedFiles = allFiles;

                        if(!string.IsNullOrEmpty(CurrentSearchText) || _selectedTagNames.Count > 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() => _ = TriggerSearchWithDebounceAsync());
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _dialogService.ShowMessage($"Ошибка фонового потока: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }, token);

                WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(false, "Показаны первые 3 000 файлов. Уточните поиск."));
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
        private async Task TriggerSearchWithDebounceAsync()
        {
            try
            {
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var token = _searchCts.Token;

                if (!string.IsNullOrEmpty(CurrentSearchText))
                    await Task.Delay(150, token);

                if (_cachedFiles.Count == 0) return;

                IEnumerable<FileRecord> query = _cachedFiles
                    .Where(f => f.Path.StartsWith(_currentActivePath));

                if (_selectedTagNames.Count > 0)
                {
                    foreach (var tagName in _selectedTagNames)
                    {
                        query = query.Where(f => f.Tags.Any(t => t.Name == tagName));
                    }
                }

                if (!string.IsNullOrWhiteSpace(CurrentSearchText))
                {
                    string[] keywords = CurrentSearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var keyword in keywords)
                    {
                        query = query.Where(f => f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                    }
                }

                token.ThrowIfCancellationRequested();

                var resultFiles = query.Take(3000).ToList();

                token.ThrowIfCancellationRequested();

                if (Files == null)
                    Files = new ObservableCollection<FileRecord>(resultFiles);
                else
                {
                    Files.Clear();

                    foreach (var file in resultFiles)
                        Files.Add(file);
                }

                WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(false, $"Найдено файлов: {resultFiles.Count}"));

                if (!string.IsNullOrWhiteSpace(CurrentSearchText))
                    WeakReferenceMessenger.Default.Send(new SearchToSaveMessage(CurrentSearchText));
            }
            catch (OperationCanceledException)
            {

            }
        }

        private bool CanSearch() => !string.IsNullOrEmpty(CurrentSearchText) || _selectedTagNames.Count > 0;


        partial void OnCurrentSearchTextChanged(string value)
        {
            _ = TriggerSearchWithDebounceAsync();
        }

        public async void Receive(FolderChangedMessage message)
        {
            try
            {
                _currentActivePath = message.newPath;

                _folderCts?.Cancel();
                _folderCts = new CancellationTokenSource();
                var token = _folderCts.Token;

                _selectedTagNames.Clear();
                SelectedFiles.Clear();
                _cachedFiles.Clear();

                if(CurrentSearchText != null) CurrentSearchText = string.Empty;
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
            }

            foreach (var file in newFiles)
            {
                if (Files?.Count < 3000) Files.Add(file);
                _cachedFiles.Add(file);
            }
        }
    }
}
//private async Task LoadFilesForPath(string path)
//{
//    if (string.IsNullOrEmpty(path) || path == "Не выбрана")
//    {
//        if (Files == null) Files = new ObservableCollection<FileRecord>();
//        _cachedFiles.Clear();
//        return;
//    }

//    _currentSessionId++;
//    int loadSessionId = _currentSessionId;
//    _globalCts?.Cancel();
//    _globalCts = new CancellationTokenSource();
//    var token = _globalCts.Token;
//    _isLoading = true;
//    _hasPendingFilter = false;

//    try
//    {
//        using var context = await _contextFactory.CreateDbContextAsync(token);

//        var totalCount = await context.Files
//            .AsNoTracking()
//            .Where(f => f.Path.StartsWith(path))
//            .CountAsync();

//        if (totalCount == 0)
//        {
//            Files = new();
//            _cachedFiles.Clear();
//            return;
//        }

//        var initialFiles = await context.Files
//            .AsNoTracking()
//            .Include(f => f.Tags)
//            .Where(f => f.Path.StartsWith(path))
//            .Take(101)
//            .ToListAsync(token);

//        bool hasMoreFiles = initialFiles.Count > 100;

//        var firstChunk = hasMoreFiles
//            ? initialFiles.Take(100).ToList()
//            : initialFiles;

//        _cachedFiles = [.. firstChunk];

//        Files = new ObservableCollection<FileRecord>(firstChunk);

//        if (hasMoreFiles)
//        {
//            _ = Task.Run(async () =>
//            {
//                try
//                {
//                    using var bgContext = await _contextFactory.CreateDbContextAsync(token);

//                    var fileStream = bgContext.Files
//                        .AsNoTracking()
//                        .Include(f => f.Tags)
//                        .Where(f => f.Path.StartsWith(path))
//                        .Skip(100)
//                        .AsAsyncEnumerable();

//                    var currentChunk = new List<FileRecord>();
//                    int ChunkSize = totalCount > 10000 ? 2000 : 500;

//                    await foreach (var file in fileStream.WithCancellation(token))
//                    {
//                        if (token.IsCancellationRequested || loadSessionId != _currentSessionId)
//                            return;

//                        currentChunk.Add(file);

//                        if (currentChunk.Count >= ChunkSize)
//                        {
//                            var chunkToSend = currentChunk;
//                            currentChunk = new List<FileRecord>();

//                            if (loadSessionId != _currentSessionId) return;

//                            _cachedFiles.AddRange(chunkToSend);

//                            await Application.Current.Dispatcher.InvokeAsync(() =>
//                            {
//                                if (loadSessionId != _currentSessionId) return;

//                                foreach (var f in chunkToSend)
//                                    Files.Add(f);
//                            }, System.Windows.Threading.DispatcherPriority.Background);

//                            await Task.Delay(10);
//                        }
//                    }

//                    if (currentChunk.Count > 0 && !token.IsCancellationRequested && loadSessionId == _currentSessionId)
//                    {
//                        _cachedFiles.AddRange(currentChunk);

//                        await Application.Current.Dispatcher.InvokeAsync(() =>
//                        {
//                            if (loadSessionId != _currentSessionId) return;

//                            foreach (var f in currentChunk)
//                                Files.Add(f);
//                        }, System.Windows.Threading.DispatcherPriority.Background);
//                    }
//                }
//                catch (OperationCanceledException) { }
//            }, token);
//        }
//    }
//    catch (OperationCanceledException) { }
//    catch (Exception ex)
//    {
//        _dialogService.ShowMessage($"Ошибка загрузки файлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
//    }
//    finally
//    {
//        _isLoading = false;
//        if (_hasPendingFilter)
//        {
//            _hasPendingFilter = false;
//            ApplyFiltersInternal();
//        }
//    }
//}

//TODO: Зависает строка поиск при вводе/стирании первых букв
//TODO: При показе файлов с тегами (фильтр через сохраненный поиск или теги) и переходе на след папку сканирование файлов тормозит
//TODO: при загрузке список меняет значения

//[RelayCommand(CanExecute = nameof(CanApplyFilters))]
//private void ApplyFilters()
//{
//    if (_isLoading)
//    {
//        _hasPendingFilter = true;
//        return;
//    }

//    ApplyFiltersInternal();
//}

//private void ApplyFiltersInternal()
//{
//    if (string.IsNullOrWhiteSpace(CurrentSearchText) && _selectedTagNames.Count == 0)
//    {
//        if (Files != null && Files.Count == _cachedFiles.Count)
//            return;

//        lock (_cacheLock)
//        {
//            if (Files != null)
//            {
//                Files.Clear();
//                foreach (var file in _cachedFiles)
//                    Files.Add(file);
//            }
//            else
//                Files = new ObservableCollection<FileRecord>(_cachedFiles);
//        }

//        return;
//    }

//    _currentSessionId++;
//    _globalCts?.Cancel();

//    lock (_cacheLock)
//    {
//        IEnumerable<FileRecord> query = _cachedFiles.ToList();

//        if (_selectedTagNames.Count > 0)
//        {
//            query = query.Where(f => _selectedTagNames.All(selectedTag =>
//                f.Tags.Any(t => t.Name.Equals(selectedTag, StringComparison.OrdinalIgnoreCase))));
//        }

//        if (!string.IsNullOrWhiteSpace(CurrentSearchText))
//        {
//            string[] keywords = CurrentSearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
//            query = query.Where(f => keywords.All(keyword =>
//                f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
//        }

//        if (Files != null)
//        {
//            Files.Clear();

//            foreach (var file in query)
//                Files.Add(file);
//        }
//        else
//            Files = new ObservableCollection<FileRecord>(query);

//        WeakReferenceMessenger.Default.Send(new SearchToSaveMessage(CurrentSearchText));
//    }
//}