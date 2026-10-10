using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Tagger.messages;
using Tagger.services.implementations;
using Tagger.services.interfaces;
using Tagger.viewmodel.FileViewModels;

namespace Tagger.viewmodel.Search
{
    public partial class SearchViewModel : ObservableObject, IFolderConsumer
    {
        private readonly IFileService _fileService;
        private readonly AppSettingsService _appSettingsService;
        private readonly ITagCacheService _tagCacheService;

        private CancellationTokenSource? _searchCts;
        private HashSet<int> _selectedTags = [];
        private string _currentActivePath = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSearching))]
        [NotifyCanExecuteChangedFor(nameof(TriggerSearchWithDebounceCommand))]
        private string _currentSearchText;

        public bool IsSearching => !string.IsNullOrWhiteSpace(CurrentSearchText) || _selectedTags.Count > 0;

        public SearchViewModel(IFileService fileService,
                               AppSettingsService appSettingsService,
                               ITagCacheService tagCacheService)
        {
            _fileService = fileService;
            _appSettingsService = appSettingsService;
            _tagCacheService = tagCacheService;
            _currentActivePath = _appSettingsService.GetSetting("FolderPath");

            RegisterMessages();
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<ApplyTagToSearch>(this, async (r, m) =>
            {
                _selectedTags = m.tagIds;
                OnPropertyChanged(nameof(IsSearching));
                await TriggerSearchWithDebounceAsync();
            });

            WeakReferenceMessenger.Default.Register<ApplySavedSearchMessage>(this, (r, m) =>
            {
                CurrentSearchText = m.search?.QueryText ?? string.Empty;
            });
        }

        partial void OnCurrentSearchTextChanged(string value)
        {
            //if (string.IsNullOrWhiteSpace(value)) return;
            TriggerSearchWithDebounceCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private async Task TriggerSearchWithDebounceAsync()
        {
            try
            {
                _searchCts?.Cancel();
                _searchCts?.Dispose();
                _searchCts = new CancellationTokenSource();

                var token = _searchCts.Token;

                await Task.Delay(150, token);
                token.ThrowIfCancellationRequested();

                var resultFiles = await _fileService.SearchWithDebounceAsync(CurrentSearchText, _currentActivePath, _selectedTags, token);
                token.ThrowIfCancellationRequested();

                var globalUiTags = await _tagCacheService.GetOrCreateCacheAsync();
                token.ThrowIfCancellationRequested();

                var viewModelResultFiles = resultFiles
                    .Select(f => new FileItemViewModel(f, globalUiTags, () => []))
                    .ToList();

                WeakReferenceMessenger.Default.Send(new SearchResultAvailableMessage(viewModelResultFiles));

                if (!string.IsNullOrWhiteSpace(CurrentSearchText))
                {
                    WeakReferenceMessenger.Default.Send(new SearchToSaveMessage(CurrentSearchText));
                }
            }
            catch (OperationCanceledException) { }
        }

        public ValueTask OnFolderChanged(string path)
        {
            _currentActivePath = path;
            return ValueTask.CompletedTask;
        }
    }
}
