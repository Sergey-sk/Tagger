using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Tagger.messages;
using Tagger.services.implementations;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.FileViewModels
{
    public partial class FolderWorkspaceViewModel : ObservableObject, IFolderConsumer
    {
        private readonly IFileService _fileService;
        private readonly IDialogService _dialogService;
        private readonly AppSettingsService _appSettingsService;
        private readonly ITagCacheService _tagCacheService;

        private string _currentActivePath = string.Empty;
        private CancellationTokenSource? _folderCts;

        [ObservableProperty]
        private bool _isLoading;

        public FolderWorkspaceViewModel(AppSettingsService appSettingsService,
                                        IFileService fileService,
                                        IDialogService dialogService,
                                        ITagCacheService tagCacheService)
        {
            _fileService = fileService;
            _dialogService = dialogService;
            _tagCacheService = tagCacheService;
            _appSettingsService = appSettingsService;
            _currentActivePath = _appSettingsService.GetSetting("FolderPath");

            WeakReferenceMessenger.Default.Register<FilesScannedBatchMessage>(this, async (r, m) =>
            {
                await RefreshFolderAsync(_currentActivePath);
            });

            WeakReferenceMessenger.Default.Register<ScanStateChangedMessage>(this, async (r, message) =>
            {
                await OnScanStateChanged(message);
            });
        }

        private async Task RefreshFolderAsync(string path)
        {
            if (IsLoading) return;

            _folderCts?.Cancel();
            _folderCts?.Dispose();
            _folderCts = new CancellationTokenSource();

            await LoadFilesAsync(path, _folderCts.Token);
        }

        private async Task LoadFilesAsync(string path, CancellationToken token)
        {
            if (string.IsNullOrEmpty(path) || path == "Не выбрана" || path == "Не указана")
            {
                WeakReferenceMessenger.Default.Send(new SearchResultAvailableMessage([]));
                return;
            }

            IsLoading = true;
            try
            {
                var globalUiTags = await _tagCacheService.GetOrCreateCacheAsync();
                token.ThrowIfCancellationRequested();

                var initialFiles = await _fileService.LoadFirstBatchFilesAsync(path, token);
                token.ThrowIfCancellationRequested();

                var viewModelFiles = initialFiles
                    .Select(f => new FileItemViewModel(f, globalUiTags, () => []))
                    .ToList();

                WeakReferenceMessenger.Default.Send(new SearchResultAvailableMessage(viewModelFiles));

                string status = viewModelFiles.Count == 0
                    ? "Файлы не найдены. Просканируйте папку."
                    : $"Показаны первые {viewModelFiles.Count} файлов. Уточните поиск.";

                WeakReferenceMessenger.Default.Send(new ChangeProgressStatus(false, status));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async ValueTask OnFolderChanged(string path)
        {
            _currentActivePath = path;
            await RefreshFolderAsync(path);
        }
        private async Task OnScanStateChanged(ScanStateChangedMessage message)
        {
            try
            {
                switch (message.status)
                {
                    case ScanStatus.Started:
                    case ScanStatus.CanceledByFolderChange:
                        WeakReferenceMessenger.Default.Send(new SearchResultAvailableMessage([]));
                        IsLoading = true;
                        break;

                    case ScanStatus.CanceledByUser:
                        IsLoading = false;
                        break;

                    case ScanStatus.Finished:
                        IsLoading = false;

                        await RefreshFolderAsync(message.folderPath);
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
    }
}
