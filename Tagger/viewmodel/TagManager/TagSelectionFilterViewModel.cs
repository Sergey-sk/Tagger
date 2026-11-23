using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using Tagger.messages;
using Tagger.services.interfaces;

namespace Tagger.viewmodel.TagManager
{
    public partial class TagSelectionFilterViewModel : ViewModelBase, IFolderConsumer
    {
        private readonly ITagStateContext _tagStateContext;

        public ObservableCollection<TagItemViewModel> SelectedTags { get => _tagStateContext.SelectedTags; set => _tagStateContext.SelectedTags = value; }
        public string CurrentFilterStr
        {
            get
            {
                return SelectedTags.Count > 0
                    ? string.Join(", ", SelectedTags.Select(t => t.Name))
                    : "Ничего не выбрано";
            }
        }

        public TagSelectionFilterViewModel(ITagStateContext tagStateContext)
        {
            _tagStateContext = tagStateContext;

            SelectedTags.CollectionChanged += (s, e) =>
            {
                WeakReferenceMessenger.Default.Send(new ApplyTagToSearch([.. SelectedTags.Select(t => t.Id)]));
                OnPropertyChanged(nameof(CurrentFilterStr));
            };
        }

        private void ResetSelectedTags()
        {
            SelectedTags.Clear();
            OnPropertyChanged(nameof(CurrentFilterStr));
        }

        public ValueTask OnFolderChanged(string path)
        {
            ResetSelectedTags();

            return ValueTask.CompletedTask;
        }
    }
}
