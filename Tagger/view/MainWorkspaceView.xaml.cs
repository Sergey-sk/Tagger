using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Tagger.view
{
    /// <summary>
    /// Логика взаимодействия для MainWorkspaceView.xaml
    /// </summary>
    public partial class MainWorkspaceView : UserControl
    {
        public MainWorkspaceView()
        {
            InitializeComponent();
        }

        public ListView FileListView => listView;
    }
}
