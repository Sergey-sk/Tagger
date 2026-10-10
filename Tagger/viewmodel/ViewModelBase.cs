using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using Tagger.messages;
using Tagger.services.interfaces;

namespace Tagger.viewmodel
{
    public class ViewModelBase:ObservableObject
    {
        protected ViewModelBase()
        {
            if(this is IFolderConsumer folderConsumer)
            {
                WeakReferenceMessenger.Default.Register<FolderChangedMessage>(this, async (r, m) => 
                    await folderConsumer.OnFolderChanged(m.newPath));
            }
        }
    }
}
