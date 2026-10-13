using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.FileViewModels
{
    public partial class FileGridViewModel : ObservableObject, IFileCollectionController
    {
        private readonly IFileService _fileService;
        private readonly ITagCacheService _tagCacheService;
        private readonly IDialogService _dialogService;

        private Dictionary<int, TagItemViewModel> _globalUiTags = [];

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _files = [];

        public ObservableCollection<FileItemViewModel> SelectedFiles { get; set; } = [];
        public long SelectedFilesSize => SelectedFiles.Sum(f => f.Size);

        public FileGridViewModel(IFileService fileService, ITagCacheService tagCacheService, IDialogService dialogService)
        {
            _fileService = fileService;
            _tagCacheService = tagCacheService;
            _dialogService = dialogService;

            SelectedFiles.CollectionChanged += OnSelectedFilesCollectionChanged;

            RegisterMessages();
        }

        private void RegisterMessages()
        {
            WeakReferenceMessenger.Default.Register<SearchResultAvailableMessage>(this, (r, m) =>
            {
                Files.Clear();
                foreach (var file in m.foundFiles)
                    Files.Add(file);
            });

            WeakReferenceMessenger.Default.Register<ApplyTagMessage>(this, async (r, message) =>
                await OnApplyTag(message));

            WeakReferenceMessenger.Default.Register<RemoveTagMessage>(this, async (r, message) =>
                await OnRemoveTag(message));

            WeakReferenceMessenger.Default.Register<DetachTagMessage>(this, async (r, message) =>
                await DetachTagFromUIFiles(message.tagId, message.detachedFiles));
        }

        private void OnSelectedFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var currentSelection = SelectedFiles.ToList();
            OnPropertyChanged(nameof(SelectedFilesSize));

            int previousCount = SelectedFiles.Count - (e.NewItems?.Count ?? 0) + (e.OldItems?.Count ?? 0);

            WeakReferenceMessenger.Default.Send(new SelectedItemsChangedMessage(currentSelection, previousCount));
        }

        /// <summary>
        /// Вызывается при перетаскивании файла на тег или применении файла к тегу
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task OnApplyTag(ApplyTagMessage message)
        {
            _globalUiTags = await _tagCacheService.GetOrCreateCacheAsync();

            if (!_globalUiTags.ContainsKey(message.tagIds.FirstOrDefault()))
            {
                _globalUiTags = await _tagCacheService.Update();
            }

            var savedTags = await _fileService.GetTagsByIds(message.tagIds);
            if (savedTags == null || savedTags.Count == 0) return;

            foreach (var tag in savedTags)
            {
                if (!_globalUiTags.TryGetValue(tag.Id, out var uiTag)) return;

                var fileIdsSet = message.fileIds.ToHashSet();

                var uiFilesToUpdate = Files
                    .Where(f => fileIdsSet.Contains(f.Id))
                    .ToList();

                foreach (var uiFile in uiFilesToUpdate)
                {
                    if (!uiFile.Tags.Any(t => t.Id == tag.Id))
                        uiFile.Tags.Add(uiTag);
                }
            }

            WeakReferenceMessenger.Default.Send(new ApplyTagToSearch([]));
        }

        private async Task OnRemoveTag(RemoveTagMessage message)
        {
            var selectedTags = await _tagCacheService.GetSelectedTags();
            selectedTags.RemoveAll(t => message.tagIds.Contains(t.Id));

            WeakReferenceMessenger.Default.Send(new ApplyTagToSearch([]));
        }

        private async Task DetachTagFromUIFiles(int tagId, HashSet<int> fileIdsSet)
        {
            _globalUiTags = await _tagCacheService.GetOrCreateCacheAsync();

            if (!_globalUiTags.TryGetValue(tagId, out var uiTag)) return;

            var uiFilesToUpdate = Files
                .Where(f => fileIdsSet.Contains(f.Id))
                .ToList();

            foreach (var uiFile in uiFilesToUpdate)
            {
                uiFile.Tags.Remove(uiTag);
            }
        }

        public void AppendTagsToUiFiles(List<int> fileIds, List<TagItemViewModel> uiTags, List<Tag> dbTags)
        {
            var uiFiles = Files.Where(f => fileIds.Contains(f.Id)).ToList();
            foreach (var uiFile in uiFiles)
            {
                foreach (var uiTag in uiTags)
                {
                    if (!uiFile.Tags.Any(t => t.Id == uiTag.Id))
                        uiFile.Tags.Add(uiTag);
                }
            }
        }

        [RelayCommand]
        public void DetachTagsFromFile(FileItemViewModel file)
        {
            var result = _dialogService.ShowMessage("Открепить все теги от этого файла?",
                                       "Открепление тегов",
                                       System.Windows.MessageBoxButton.YesNo,
                                       System.Windows.MessageBoxImage.Question);

            if(result == System.Windows.MessageBoxResult.No) return;

            _fileService.DetachAllTagsFromFilesAsync(file.Id);

            WeakReferenceMessenger.Default.Send(new DetachAllTagMessage(file));

            var uiFile = Files.FirstOrDefault(f => f.Id == file.Id);
            if (uiFile != null)
                uiFile.Tags.Clear();
        }
    }
}
