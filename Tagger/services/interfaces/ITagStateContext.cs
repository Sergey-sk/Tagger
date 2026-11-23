using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Tagger.model;
using Tagger.viewmodel;
using Tagger.viewmodel.FileViewModels;

namespace Tagger.services.interfaces
{
    public interface ITagStateContext: INotifyPropertyChanged
    {
        ObservableCollection<TagItemViewModel> Tags { get; set; }
        ObservableCollection<TagItemViewModel> SelectedTags { get; set; }
        List<FileItemViewModel> SelectedFiles { get; set; }
        string SearchText { get; set; }

        void AddTag(TagItemViewModel tag);
        void AddTag(Tag tag);
        void RemoveTags(HashSet<int> tagIds);
        void UpdateFileCountForTag(int tagId, int delta);
        void RemoveSelectedTags(List<TagItemViewModel> tags);
    }
}
