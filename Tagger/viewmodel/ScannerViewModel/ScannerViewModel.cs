using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.IO;
using System.Windows;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.ScannerViewModel
{
    public partial class ScannerViewModel : ObservableObject, IRecipient<FolderChangedMessage>
    {
        private readonly IDialogService _dialogService;
        private readonly IScanningService _scanningService;

        private string _currentActivePath;
        private bool _isCancelingByFolderChange;
        private bool _changeStatus;

        [ObservableProperty]
        private string _progressStatus = "Готов к сканированию";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
        private bool _isScaning;

        public ScannerViewModel(IDialogService dialogService, IScanningService scanningService)
        {
            _dialogService = dialogService;
            _scanningService = scanningService;

            WeakReferenceMessenger.Default.Register(this);

            WeakReferenceMessenger.Default.Register<ChangeProgressStatus>(this, (r, message) =>
            {
                IsScaning = message.isLoading;
                ProgressStatus = message.value;
                _changeStatus = false;
            });

            _currentActivePath = Properties.Settings.Default.FolderPath;
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
                    totalScannedCount += files.Count;
                    if (_changeStatus) ProgressStatus = $"Найдено файлов: {totalScannedCount:N0}";
                    WeakReferenceMessenger.Default.Send(new FilesScannedBatchMessage(files));
                }
            });

            try
            {
                WeakReferenceMessenger.Default.Send(new ScanStateChangedMessage(ScanStatus.Started, _currentActivePath));

                await _scanningService.ScanDirectoryAsync(_currentActivePath, progress, token);

                WeakReferenceMessenger.Default.Send(new ScanStateChangedMessage(ScanStatus.Finished, _currentActivePath));
                ProgressStatus = "Сканирование успешно завершено!";
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
            Directory.Exists(_currentActivePath) &&
            !string.IsNullOrEmpty(_currentActivePath)
            && _currentActivePath != "Не выбрана";

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
