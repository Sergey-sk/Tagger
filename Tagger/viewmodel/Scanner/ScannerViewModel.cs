using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Tagger.messages;
using Tagger.services.implementations;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.ScannerViewModel
{
    public partial class ScannerViewModel : ViewModelBase, IFolderConsumer
    {
        private readonly IDialogService _dialogService;
        private readonly IScanningService _scanningService;
        private readonly AppSettingsService _appSettingsService;
        private string _currentActivePath;
        private bool _isCancelingByFolderChange;
        private bool _changeStatus;
        private string _lastSuccessScaningTime;
        private CancellationTokenSource _backgroundScanToken;

        [ObservableProperty]
        private string _progressStatus = "Готов к сканированию";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
        private bool _isScaning;

        public ScannerViewModel(IDialogService dialogService, IScanningService scanningService, AppSettingsService appSettingsService)
        {
            _dialogService = dialogService;
            _scanningService = scanningService;
            _appSettingsService = appSettingsService;

            RegisterMessages();

            _currentActivePath = _appSettingsService.GetSetting("FolderPath");
            _lastSuccessScaningTime = _appSettingsService.GetSetting("LastSuccessScanTime");
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<ChangeProgressStatus>(this, (r, message) =>
            {
                IsScaning = message.isLoading;
                ProgressStatus = message.value;
                _changeStatus = false;
            });

            WeakReferenceMessenger.Default.Register<StartBackroundScan>(this, async (r, m) =>
                await BackgroundScanAsync());

            WeakReferenceMessenger.Default.Register<ApplyChangesFiles>(this, async (r, m) =>
                await ApplyChanges());
        }

        [RelayCommand(CanExecute = nameof(ScanCanExecute), IncludeCancelCommand = true)]
        private async Task StartScanAsync(CancellationToken token)
        {
            var sw = Stopwatch.StartNew();

            if (string.IsNullOrEmpty(_currentActivePath)) return;

            if (!Directory.Exists(_currentActivePath))
            {
                _dialogService.ShowMessage("Папка не найдена", "Внимание!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _backgroundScanToken?.Cancel();
            IsScaning = true;
            _changeStatus = true;
            _isCancelingByFolderChange = false;
            ProgressStatus = "Начало сканирования";

            var progress = new Progress<int>(filesCount =>
            {
                if (filesCount > 0)
                {
                    if (_changeStatus) ProgressStatus = $"Найдено файлов: {filesCount:N0}";
                    WeakReferenceMessenger.Default.Send(new FilesScannedBatchMessage(filesCount));
                }
            });

            try
            {
                WeakReferenceMessenger.Default.Send(new ScanStateChangedMessage(ScanStatus.Started, _currentActivePath));

                await Task.Run(async () =>
                {
                    await _scanningService.ScanDirectoryAsync(_currentActivePath, _lastSuccessScaningTime, progress, token);
                }, token);

                WeakReferenceMessenger.Default.Send(new ScanStateChangedMessage(ScanStatus.Finished, _currentActivePath));
                ProgressStatus = "Сканирование успешно завершено!";

                _lastSuccessScaningTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _appSettingsService.SetSetting("LastSuccessScanTime", _lastSuccessScaningTime);
            }
            catch (OperationCanceledException)
            {
                if (_isCancelingByFolderChange)
                {
                    WeakReferenceMessenger.Default.Send(new ScanStateChangedMessage(ScanStatus.CanceledByFolderChange, _currentActivePath));
                    ProgressStatus = "Сканирование прервано";
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new ScanStateChangedMessage(ScanStatus.CanceledByUser, _currentActivePath));
                    ProgressStatus = "Сканирование отменено";
                }
            }
            catch (Exception ex)
            {
                ProgressStatus = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsScaning = false;

                sw.Stop();
                Debug.WriteLine($"Прошло: {sw.Elapsed.TotalMilliseconds} мс");
                Debug.WriteLine($"Прошло: {sw.Elapsed}");
            }
        }

        [RelayCommand(CanExecute = nameof(ScanCanExecute))]
        private async Task BackgroundScanAsync()
        {
            _backgroundScanToken?.Cancel();
            _backgroundScanToken = new CancellationTokenSource();
            var token = _backgroundScanToken.Token;

            var rootPath = Environment.GetEnvironmentVariable("SystemDrive") + "\\";

            try
            {
                await Task.Run(async () =>
                {
                    await _scanningService.ScanDirectoryAsync(rootPath, _lastSuccessScaningTime, null, token, true);
                }, token);

                _lastSuccessScaningTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _appSettingsService.SetSetting("LastSuccessScanTime", _lastSuccessScaningTime);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ProgressStatus = $"Ошибка: {ex.Message}";
            }

        }

        private async Task ApplyChanges()
        {
            ProgressStatus = "Применение изменений...";
            IsScaning = true;

            var rootPath = Environment.GetEnvironmentVariable("SystemDrive") + "\\";

            try
            {
                await Task.Run(async () =>
                {
                    await _scanningService.ApplyBackgroundScanChangesAsync(rootPath, CancellationToken.None);
                });

                WeakReferenceMessenger.Default.Send(new ReloadFilesMessage());

                ProgressStatus = "Изменения успешно применены!";
            }
            catch (Exception ex)
            {
                ProgressStatus = $"Ошибка при применении изменений: {ex.Message}";
            }
            finally
            {
                IsScaning = false;
            }
        }

        private bool ScanCanExecute() =>
            !IsScaning &&
            !string.IsNullOrEmpty(_currentActivePath) &&
            Directory.Exists(_currentActivePath) &&
            _currentActivePath != "Не выбрана" &&
            _currentActivePath != "Не указана";

        public ValueTask OnFolderChanged(string path)
        {
            _currentActivePath = path;

            if (StartScanCommand.IsRunning)
            {
                StartScanCancelCommand.Execute(null);
                _isCancelingByFolderChange = true;
            }

            StartScanCommand.NotifyCanExecuteChanged();

            return ValueTask.CompletedTask;
        }
    }
}
