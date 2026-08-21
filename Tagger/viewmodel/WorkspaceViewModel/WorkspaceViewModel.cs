using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.WorkspaceViewModel
{
    public partial class WorkspaceViewModel : ObservableObject
    {
        private readonly IDialogService _dialogService;
        private readonly ISavedSearchService _savedSearchService;

        private string _searchText;

        [ObservableProperty]
        private string _currentPath;

        [ObservableProperty]
        private ObservableCollection<SavedSearch> _savedSearch;

        [ObservableProperty]
        private SavedSearch _selectedSavedSearch;

        [ObservableProperty]
        private string _selectedQuickAccessPath;

        [ObservableProperty]
        private ObservableCollection<string> _quickAccess = new();

        public WorkspaceViewModel(IDialogService dialogService,
                                  ISavedSearchService savedSearchService)
        {
            _dialogService = dialogService;
            _savedSearchService = savedSearchService;

            LoadQuickAccess();

            CurrentPath = Directory.Exists(Properties.Settings.Default.FolderPath)
                ? Properties.Settings.Default.FolderPath
                : "Не указана";

            WeakReferenceMessenger.Default.Register<SearchToSaveMessage>(this, (r, message) =>
            {
                _searchText = message.search;
            });
        }

        private void LoadQuickAccess()
        {
            string rawFolders = Properties.Settings.Default.RecentFoldersRaw;

            if (!string.IsNullOrEmpty(rawFolders))
            {
                var folders = rawFolders.Split(';', StringSplitOptions.RemoveEmptyEntries);
                QuickAccess = new ObservableCollection<string>();

                foreach (var folder in folders)
                {
                    if (Directory.Exists(folder))
                        QuickAccess.Add(folder);
                }

            }
            else
            {
                QuickAccess = new ObservableCollection<string>
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                };
            }
        }

        partial void OnCurrentPathChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "Не выбрана") return;

            LoadSearchForPathCommand.ExecuteAsync(value);

            if (!QuickAccess.Contains(value))
            {
                QuickAccess.Insert(0, value);

                if (QuickAccess.Count > 5)
                    QuickAccess.RemoveAt(QuickAccess.Count - 1);
            }
            else
            {
                QuickAccess.Move(QuickAccess.IndexOf(value), 0);
            }

            Properties.Settings.Default.FolderPath = value;
            Properties.Settings.Default.RecentFoldersRaw = string.Join(";", QuickAccess);
            Properties.Settings.Default.Save();

            WeakReferenceMessenger.Default.Send(new FolderChangedMessage(value));
        }

        partial void OnSelectedSavedSearchChanged(SavedSearch value)
        {
            WeakReferenceMessenger.Default.Send(new ApplySavedSearchMessage(value));
        }

        partial void OnSelectedQuickAccessPathChanged(string value)
        {
            if (CurrentPath != value && !string.IsNullOrEmpty(value))
                CurrentPath = value;
        }

        [RelayCommand]
        private async Task LoadSearchForPathAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var filteredSearches = await _savedSearchService.LoadSavedSearchesForPathAsync(path);

            if (SavedSearch == null)
                SavedSearch = new ObservableCollection<SavedSearch>(filteredSearches);
            else
            {
                SavedSearch.Clear();
                foreach (var search in filteredSearches)
                    SavedSearch.Add(search);
            }
        }

        [RelayCommand]
        private void ChooseFolder()
        {
            if (_dialogService.ShowOpenFolderDialog())
            {
                CurrentPath = _dialogService.FolderPath;
            }
        }

        [RelayCommand]
        private async Task SaveCurrentSearch()
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                _dialogService.ShowMessage("Введите поиск для сохранения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (SavedSearch.Any(s => s.QueryText == _searchText))
            {
                _dialogService.ShowMessage("Поиск уже сохранен", "Внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var newSearch = new SavedSearch { QueryText = _searchText, FolderPath = CurrentPath };

            await _savedSearchService.AddSearchAsync(newSearch);

            SavedSearch.Add(newSearch);
            _selectedSavedSearch = newSearch;
            OnPropertyChanged(nameof(SelectedSavedSearch));
        }

        [RelayCommand]
        private async Task RemoveSavedSearch(SavedSearch search)
        {
            var result = _dialogService.ShowMessage("Удалить сохраненный поиск?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes && search != null)
            {
                await _savedSearchService.RemoveSearchAsync(search);
                SavedSearch.Remove(search);
            }
        }
    }
}
