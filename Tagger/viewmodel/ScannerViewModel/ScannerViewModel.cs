using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Windows;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.ScannerViewModel
{
    public partial class ScannerViewModel : ObservableObject, IRecipient<FolderChangedMessage> //все что касается сканирования
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IDialogService _dialogService;

        private string _currentActivePath;
        private bool _isCancelingByFolderChange;

        [ObservableProperty]
        private string _progressStatus = "Готов к сканированию";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
        private bool _isScaning;

        public ScannerViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, IDialogService dialogService)
        {
            _contextFactory = contextFactory;
            _dialogService = dialogService;

            WeakReferenceMessenger.Default.Register(this);

            WeakReferenceMessenger.Default.Register<ChangeProgressStatus>(this, (r, message) =>
            {
                IsScaning = message.isLoading;
                ProgressStatus = message.value;
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
            _isCancelingByFolderChange = false;
            ProgressStatus = "Начало сканирования";
            int totalScannedCount = 0;

            var progress = new Progress<List<FileRecord>>(files =>
            {
                if (files.Count > 0)
                {
                    totalScannedCount += files.Count;
                    ProgressStatus = $"Найдено файлов: {totalScannedCount:N0}";
                    WeakReferenceMessenger.Default.Send(new FilesScannedBatchMessage(files));
                }
            });

            try
            {
                await Scan(_currentActivePath, progress, token);

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

        private async Task Scan(string dirPath, IProgress<List<FileRecord>> uiProgress, CancellationToken token)
        {
            WeakReferenceMessenger.Default.Send(new ScanStateChangedMessage(ScanStatus.Started, dirPath));

            await Task.Run(async () =>
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.ChangeTracker.AutoDetectChangesEnabled = false;

                try
                {
                    int totalFilesCount = 0;
                    const int DbBatchSize = 2000;
                    const int UiBatchSize = 2000;

                    var dbBatchList = new List<FileRecord>();
                    var uiBatchList = new List<FileRecord>();
                    var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var existingPath = new HashSet<string>(
                        await context.Files
                        .Where(f => f.Path.StartsWith(dirPath))
                        .Select(f => f.Path)
                        .ToListAsync(token),
                        StringComparer.OrdinalIgnoreCase);

                    var rootDir = new DirectoryInfo(dirPath);
                    var enumerationOptions = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                    };

                    foreach (var file in rootDir.EnumerateFiles("*", enumerationOptions))
                    {
                        token.ThrowIfCancellationRequested();
                        scannedPaths.Add(file.FullName);

                        if (existingPath.Contains(file.FullName)) continue;

                        var record = new FileRecord
                        {
                            Name = file.Name,
                            Path = file.FullName,
                            Size = file.Length,
                            LastModified = file.LastWriteTime,
                            IsDeleted = false
                        };

                        dbBatchList.Add(record);
                        uiBatchList.Add(record);
                        totalFilesCount++;

                        if (uiBatchList.Count >= UiBatchSize)
                        {
                            uiProgress.Report([.. uiBatchList]);
                            uiBatchList.Clear();
                        }

                        if (dbBatchList.Count >= DbBatchSize)
                        {
                            await context.Files.AddRangeAsync(dbBatchList, token);
                            await context.SaveChangesAsync(token);

                            foreach (var added in dbBatchList) existingPath.Add(added.Path);
                            dbBatchList.Clear();
                        }
                    }

                    if (uiBatchList.Count > 0)
                    {
                        uiProgress.Report([.. uiBatchList]);
                    }

                    if (dbBatchList.Count > 0)
                    {
                        await context.Files.AddRangeAsync(dbBatchList, token);
                        await context.SaveChangesAsync(token);
                    }

                    var dbFiles = await context.Files
                        .Where(f => f.Path.StartsWith(dirPath))
                        .ToListAsync(token);

                    var filesToRemove = dbFiles
                        .Where(f => !scannedPaths.Contains(f.Path))
                        .ToList();

                    if (filesToRemove.Any())
                    {
                        context.RemoveRange(filesToRemove);
                        await context.SaveChangesAsync(token);
                    }

                    uiProgress.Report([]);
                }
                finally
                {
                    context.ChangeTracker.AutoDetectChangesEnabled = true;
                }
            }, token);
        }

        private bool ScanCanExecute() =>
            !IsScaning &&
            Directory.Exists(_currentActivePath) &&
            !string.IsNullOrEmpty(_currentActivePath)
            && _currentActivePath != "Не выбрана";

        public void UpdateProgressStatus() =>
            ProgressStatus = "Готов к сканированию";

        public void Receive(FolderChangedMessage message)
        {
            _currentActivePath = message.newPath;

            if (StartScanCommand.IsRunning)
            {
                StartScanCancelCommand.Execute(null);
                _isCancelingByFolderChange = true;
            }

            ProgressStatus = "Готов к сканированию";
            StartScanCommand.NotifyCanExecuteChanged();
        }
    }
}
