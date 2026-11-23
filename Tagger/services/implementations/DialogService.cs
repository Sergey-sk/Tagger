using Microsoft.Win32;
using System.Windows;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class DialogService : IDialogService
    {
        string FolderPath { get; set; }
        string IDialogService.FolderPath { get => FolderPath; set => FolderPath = value; }

        public MessageBoxResult ShowMessage(string message, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Error)
        {
            return MessageBox.Show(message, caption, button, icon);
        }

        public bool ShowOpenFolderDialog()
        {
            var folderDialog = new OpenFolderDialog();

            if (folderDialog.ShowDialog() == true)
            {
                FolderPath = folderDialog.FolderName;
                return true;
            }

            return false;
        }
    }
}
