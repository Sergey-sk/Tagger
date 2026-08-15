using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Tagger.services.interfaces
{
    public interface IDialogService
    {
        string FolderPath { get; set; }
        MessageBoxResult ShowMessage(string message, string caption, MessageBoxButton button, MessageBoxImage icon);
        bool ShowOpenFolderDialog();
    }
}
