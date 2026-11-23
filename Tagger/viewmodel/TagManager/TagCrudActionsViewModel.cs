using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Tagger.messages;
using Tagger.services.interfaces;
using Tagger.viewmodel.FileViewModels;

namespace Tagger.viewmodel.TagManager
{
    public partial class TagCrudActionsViewModel : ObservableObject
    {
        private readonly ITagService _tagService;
        private readonly IDialogService _dialogService;
        private readonly ITagStateContext _tagStateContext;

        private List<FileItemViewModel> SelectedFiles { get => _tagStateContext.SelectedFiles; set => _tagStateContext.SelectedFiles = value; }

        public TagCrudActionsViewModel(ITagService tagService,
                                       IDialogService dialogService,
                                       ITagStateContext tagStateContext)
        {
            _tagService = tagService;
            _dialogService = dialogService;
            _tagStateContext = tagStateContext;

            WeakReferenceMessenger.Default.Register<SelectedItemsChangedMessage>(this, (r, m) =>
                SelectedFiles = m.selectedItems);
        }

        [RelayCommand]
        private async Task<bool> CreateTagAsync(string tagName)
        {
            try
            {
                var newTag = await Task.Run(() => _tagService.CreateTagAsync(tagName));
                var viewModelTag = new TagItemViewModel(newTag, () => SelectedFiles);

                _tagStateContext.AddTag(viewModelTag);
                _tagStateContext.SearchText = string.Empty;
                return true;
            }
            catch (ArgumentException ex)
            {
                _tagStateContext.SearchText = string.Empty;
                _dialogService.ShowMessage(ex.Message, "Внимание!", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        [RelayCommand]
        private async Task RemoveTagAsync(int tagId)
        {
            var selectedTags = _tagStateContext.SelectedTags;
            var tagIdsList = selectedTags.Select(t => t.Id).Count() == 0 ? [tagId] : selectedTags.Select(t => t.Id).ToList();
            var tagIds = tagIdsList.ToHashSet();

            int filesCount = await _tagService.GetFilesCountByTagIds(tagIds);
            string message;

            if (selectedTags.Count == 1)
                message = $"Удалить тег? ({filesCount} файлов)";
            else
                message = $"Удалить теги? ({filesCount} файлов)";

            var result = _dialogService.ShowMessage(message, "Удаление тегов", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.No) return;

            try
            {
                await _tagService.RemoveTagsAsync(tagIds);

                _tagStateContext.RemoveTags(tagIds);

                WeakReferenceMessenger.Default.Send(new RemoveTagMessage(tagIds));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
