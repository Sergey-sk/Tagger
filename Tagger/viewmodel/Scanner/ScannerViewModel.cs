using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.IO;
using System.Windows;
using Tagger.messages;
using Tagger.model;
using Tagger.services.implementations;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.ScannerViewModel
{
    public partial class ScannerViewModel : ObservableObject, IRecipient<FolderChangedMessage>
    {
        private readonly IDialogService _dialogService;
        private readonly IScanningService _scanningService;
        private readonly AppSettingsService _appSettingsService;
        private string _currentActivePath;
        private bool _isCancelingByFolderChange;
        private bool _changeStatus;
        private string _lastSuccessScaningTime;

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

            WeakReferenceMessenger.Default.Register(this);

            WeakReferenceMessenger.Default.Register<ChangeProgressStatus>(this, (r, message) =>
            {
                IsScaning = message.isLoading;
                ProgressStatus = message.value;
                _changeStatus = false;
            });

            _currentActivePath = _appSettingsService.GetSetting("FolderPath");
            _lastSuccessScaningTime = _appSettingsService.GetSetting("LastSuccessScanTime");
        }

        [RelayCommand(CanExecute = nameof(ScanCanExecute), IncludeCancelCommand = true)]
        private async Task StartScanAsync(CancellationToken token)
        {
            if (string.IsNullOrEmpty(_currentActivePath)) return;

            if (!Directory.Exists(_currentActivePath))
            {
                _dialogService.ShowMessage("Папка не найдена", "Внимание!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsScaning = true;
            _changeStatus = true;
            _isCancelingByFolderChange = false;
            ProgressStatus = "Начало сканирования";
            int totalScannedCount = 0;

            var progress = new Progress<List<FileRecord>>(files =>
            {
                if (files.Count > 0)
                {
                    totalScannedCount += (int)files[0].Size;
                    if (_changeStatus) ProgressStatus = $"Найдено файлов: {totalScannedCount:N0}";
                    WeakReferenceMessenger.Default.Send(new FilesScannedBatchMessage(files));
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
            }

        }

        private bool ScanCanExecute() =>
            !IsScaning &&
            !string.IsNullOrEmpty(_currentActivePath) &&
            Directory.Exists(_currentActivePath) &&
            _currentActivePath != "Не выбрана" &&
            _currentActivePath != "Не указана";

        public void Receive(FolderChangedMessage message)
        {
            _currentActivePath = message.newPath;

            if (StartScanCommand.IsRunning)
            {
                StartScanCancelCommand.Execute(null);
                _isCancelingByFolderChange = true;
            }

            StartScanCommand.NotifyCanExecuteChanged();
        }
    }
}
