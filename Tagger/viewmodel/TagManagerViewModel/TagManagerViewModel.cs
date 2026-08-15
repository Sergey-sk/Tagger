using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Tagger.messages;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.TagManagerViewModel
{
    public partial class TagManagerViewModel : ObservableObject // все что касается управления тегами
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<Tag> _tags;

        [ObservableProperty]
        private string _searchText;

        public ObservableCollection<Tag> SelectedTags { get; set; } = [];

        [ObservableProperty]
        private ICollectionView? _tagsView;

        private List<FileRecord> _selectedFiles = [];

        public string CurrentFilterStr
        {
            get
            {
                return SelectedTags.Count > 0
                    ? string.Join(", ", SelectedTags.Select(t => t.Name))
                    : "Ничего не выбрано";
            }
        }

        public TagManagerViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, IDialogService dialogService)
        {
            _contextFactory = contextFactory;
            _dialogService = dialogService;

            SelectedTags.CollectionChanged += (s, e) =>
            {
                var currentSelection = SelectedTags.Select(t => t.Name).ToList();
                WeakReferenceMessenger.Default.Send(new ApplyTagToSearch(currentSelection));
                OnPropertyChanged(nameof(CurrentFilterStr));
            };

            WeakReferenceMessenger.Default.Register<FolderChangedMessage>(this, (r, message) =>
            {
                ResetSelectedTags();
                _selectedFiles.Clear();
                SearchText = string.Empty;
            });

            WeakReferenceMessenger.Default.Register<SelectedItemsChangedMessage>(this, (r, message) =>
            {
                _selectedFiles = message.selectedItems;

                ApplyToSelectedFilesCommand.NotifyCanExecuteChanged();
            });
        }

        partial void OnSearchTextChanged(string value)
        {
            TagsView?.Refresh();
            CreateTagCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task LoadTagsAsync()
        {
            //if(Tags != null)
            //{
            //    foreach (var tag in Tags)
            //        tag.PropertyChanged -= Tag_PropertyChanged;
            //}
            if (SelectedTags.Count > 0) SelectedTags.Clear();
            OnPropertyChanged(nameof(CurrentFilterStr));

            using var context = await _contextFactory.CreateDbContextAsync();

            var collection = await context.Tags
                    .AsNoTracking()
                    .Include(t => t.Files)
                    .OrderByDescending(t => t.Files.Count)
                    .ToListAsync();

            Tags = new ObservableCollection<Tag>(collection);
            //foreach (var tag in Tags)
            //{
            //    tag.PropertyChanged += Tag_PropertyChanged;
            //}

            TagsView = CollectionViewSource.GetDefaultView(Tags);
            TagsView.Filter = FilterTags;

            OnPropertyChanged(nameof(CurrentFilterStr));
        }

        //TODO: при добавлении тега к файлам тег у них не показывается, только при перезаходе
        [RelayCommand(CanExecute = nameof(CanCreateTag))]
        private async Task CreateTagAsync()
        {
            string tagName = SearchText.Trim();

            if (string.IsNullOrWhiteSpace(tagName)) return;

            using var context = await _contextFactory.CreateDbContextAsync();

            var exist = await context.Tags
                .AsNoTracking()
                .AnyAsync(t => t.Name.ToLower() == tagName.ToLower());

            if (exist)
            {
                SearchText = string.Empty;
                _dialogService.ShowMessage("Тег с таким именем уже существует", "Внимание!", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var newTag = new Tag
            {
                Name = tagName,
            };

            context.Tags.Add(newTag);
            await context.SaveChangesAsync();

            //newTag.PropertyChanged += Tag_PropertyChanged;

            Tags.Add(newTag);

            newTag.IsSelected = true;

            if (!SelectedTags.Contains(newTag))
            {
                SelectedTags.Add(newTag);
                OnPropertyChanged(nameof(CurrentFilterStr));
            }

            SearchText = string.Empty;
        }

        private bool CanCreateTag() => !string.IsNullOrWhiteSpace(SearchText);

        [RelayCommand]
        private async Task RemoveTagAsync(int tagId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var tag = await context.Tags
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag != null)
            {
                context.Tags.Remove(tag);
                await context.SaveChangesAsync();

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var tagInUI = Tags.FirstOrDefault(t => t.Id == tagId);
                    if (tagInUI != null)
                    {
                        tagInUI.IsSelected = false;
                        Tags.Remove(tagInUI);
                    }
                });

                WeakReferenceMessenger.Default.Send(new RemoveTagMessage(tag.Name));
            }
        }

        private void ResetSelectedTags()
        {
            if (Tags == null || !Tags.Any()) return;

            SelectedTags.Clear();

            OnPropertyChanged(nameof(CurrentFilterStr));
        }

        //private void Tag_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        //{
        //    if (e.PropertyName == nameof(Tag.IsSelected) && sender is Tag tag)
        //    {
        //        if (tag.IsSelected)
        //            SelectedTags.Add(tag);
        //        else SelectedTags.Remove(tag);

        //        OnPropertyChanged(nameof(CurrentFilterStr));
        //        WeakReferenceMessenger.Default.Send(new ApplyTagToSearch([..SelectedTags.Select(t => t.Name)]));
        //    }
        //}

        private bool FilterTags(object obj)
        {
            if (string.IsNullOrEmpty(SearchText)) return true;

            if (obj is Tag tag)
            {
                return tag.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        [RelayCommand(CanExecute = nameof(CanApplyFilesToTag))]
        private async Task ApplyToSelectedFiles(int tagId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var tag = await context.Tags
                .Include(t => t.Files)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null) return;

            var selectedFileIds = _selectedFiles.Select(f => f.Id);

            var filesToAttach = await context.Files
                .Where(f => selectedFileIds.Contains(f.Id))
                .ToListAsync();

            foreach (var file in filesToAttach)
            {
                if (!tag.Files.Any(f => f.Id == file.Id))
                {
                    context.Files.Attach(file);
                    tag.Files.Add(file);
                }
            }

            await context.SaveChangesAsync();

            var tagInUi = Tags.FirstOrDefault(f => f.Id == tagId);
            if (tagInUi != null)
            {
                foreach (var file in filesToAttach)
                {
                    if (!tagInUi.Files.Any(f => f.Id == file.Id))
                        tagInUi.Files.Add(file);
                }
            }

            Tags = [.. Tags.OrderByDescending(t => t.Files.Count)];

            TagsView = CollectionViewSource.GetDefaultView(Tags);
            TagsView.Filter = FilterTags;
            TagsView?.Refresh();

            WeakReferenceMessenger.Default.Send(new ApplyTagMessage(tagId, selectedFileIds.ToList()));
        }

        private bool CanApplyFilesToTag() => _selectedFiles.Count > 0;
    }
}
