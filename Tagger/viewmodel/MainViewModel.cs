using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using Tagger.services.interfaces;

namespace Tagger.viewmodel
{
    public partial class MainViewModel : ObservableObject
    {
        public WorkspaceViewModel.WorkspaceViewModel Workspace { get; }
        public ScannerViewModel.ScannerViewModel Scanner { get; set; }
        public FileViewerViewModel.FileViewerViewModel FileViewer { get; }
        public TagManagerViewModel.TagManagerViewModel TagManager { get; }


        [ObservableProperty]
        private string _currentTheme = "System";

        public MainViewModel(IDialogService dialogService,
                             IDbContextFactory<ApplicationDbContext> contextFactory,
                             ITagService tagService,
                             IFileIndexingService fileIndexingService,
                             IScanningService scanningService,
                             ISavedSearchService savedSearchService,
                             IFileService fileService)
        {
            FileViewer = new FileViewerViewModel.FileViewerViewModel(contextFactory, dialogService, fileService);
            Workspace = new WorkspaceViewModel.WorkspaceViewModel(dialogService, savedSearchService);
            Scanner = new ScannerViewModel.ScannerViewModel(dialogService, scanningService);
            TagManager = new TagManagerViewModel.TagManagerViewModel( dialogService, tagService, fileIndexingService);

            CurrentTheme = Properties.Settings.Default.Theme;
        }
        partial void OnCurrentThemeChanging(string value)
        {
            var appResources = Application.Current.Resources.MergedDictionaries;

            var oldTheme = appResources.FirstOrDefault(d => d.Source != null &&
                (d.Source.OriginalString.Contains("DarkTheme") || d.Source.OriginalString.Contains("LightTheme")));

            if (oldTheme != null) appResources.Remove(oldTheme);

#pragma warning disable WPF0001 // Тип предназначен только для оценки и может быть изменен или удален в будущих обновлениях. Чтобы продолжить, скройте эту диагностику.
            if (value == "Dark")
            {
                Application.Current.ThemeMode = ThemeMode.Dark;
                appResources.Add(new ResourceDictionary { Source = new Uri("/Themes/DarkTheme.xaml", UriKind.Relative) });
            }
            else if (value == "Light")
            {
                Application.Current.ThemeMode = ThemeMode.Light;
                appResources.Add(new ResourceDictionary { Source = new Uri("/Themes/LightTheme.xaml", UriKind.Relative) });
            }
            else
            {
                Application.Current.ThemeMode = ThemeMode.System;
                bool isWindowDark = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1) as int? == 0;

                string systemTheme = isWindowDark ? "DarkTheme" : "LightTheme";

                appResources.Add(new ResourceDictionary { Source = new Uri($"/Themes/{systemTheme}.xaml", UriKind.Relative) });
            }
#pragma warning restore WPF0001 // Тип предназначен только для оценки и может быть изменен или удален в будущих обновлениях. Чтобы продолжить, скройте эту диагностику.
        }

        [RelayCommand]
        private void ChangeTheme(string theme)
        {
            CurrentTheme = theme;
            Properties.Settings.Default.Theme = theme;
            Properties.Settings.Default.Save();
        }
    }
}
