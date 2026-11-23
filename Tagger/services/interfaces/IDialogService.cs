using System.Windows;

namespace Tagger.services.interfaces
{
    public interface IDialogService
    {
        string FolderPath { get; set; }
        MessageBoxResult ShowMessage(string message, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Error);
        bool ShowOpenFolderDialog();
    }
}
