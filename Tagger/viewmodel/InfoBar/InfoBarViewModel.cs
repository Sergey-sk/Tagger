using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Text;
using Tagger.messages;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.InfoBar
{
    public partial class InfoBarViewModel : ObservableObject
    {
        private readonly IScanningService _scanningService;

        [ObservableProperty]
        private bool _isInfoBarOpen;

        [ObservableProperty]
        private string _title;

        private int _newFilesCount;
        private int _deletedFilesCount;
        private string _rootPath = Environment.GetEnvironmentVariable("SystemDrive") + "\\";


        public InfoBarViewModel(IScanningService scanningService)
        {
            _scanningService = scanningService;

            WeakReferenceMessenger.Default.Register<BackgroundScanFoundChangesMessage>(this, (r, m) =>
            {
                IsInfoBarOpen = true;
                _newFilesCount = m.newFilesCount;
                _deletedFilesCount = m.deletedFilesCount;

                var sb = new StringBuilder();

                if (_newFilesCount > 0) sb.Append($"Найдено {_newFilesCount} новых файлов.");
                if (_deletedFilesCount > 0) sb.Append($"{_deletedFilesCount} файла удаленно.");

                Title = sb.ToString();
            });
        }

        [RelayCommand]
        public void CloseInfoBar()
        {
            IsInfoBarOpen = false;
        }

        [RelayCommand]
        public void UpdateFileChanges()
        {
            WeakReferenceMessenger.Default.Send(new ApplyChangesFiles());
            IsInfoBarOpen = false;
        }
    }
}
