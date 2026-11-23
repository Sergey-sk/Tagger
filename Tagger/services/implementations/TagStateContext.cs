using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Tagger.model;
using Tagger.services.interfaces;
using Tagger.viewmodel;
using Tagger.viewmodel.FileViewModels;

namespace Tagger.services.implementations
{
    public partial class TagStateContext : ObservableObject, ITagStateContext
    {
        public ObservableCollection<TagItemViewModel> Tags { get; set; } = [];

        public ObservableCollection<TagItemViewModel> SelectedTags { get; set; } = [];
        public List<FileItemViewModel> SelectedFiles { get; set; } = [];

        [ObservableProperty]
        private string _searchText;

        public void AddTag(TagItemViewModel tag)
        {
            Tags.Add(tag);
        }

        public void AddTag(Tag tag)
        {
            Tags.Add(new TagItemViewModel(tag, () => SelectedFiles));
        }

        public void RemoveTags(HashSet<int> tagIds)
        {
            var tagsToRemove = Tags.Where(t => tagIds.Contains(t.Id)).ToList();
            foreach (var tagToRemove in tagsToRemove)
            {
                Tags.Remove(tagToRemove);
                SelectedTags.Remove(tagToRemove);
            }
        }

        public void UpdateFileCountForTag(int tagId, int delta)
        {
            var tag = Tags.FirstOrDefault(t => t.Id == tagId);
            if (tag != null)
            {
                if(delta > 0) tag.IncrementFilesCount(delta);
                else tag.DecrementFilesCount(Math.Abs(delta));
            }
        }

        public void RemoveSelectedTags(List<TagItemViewModel> tags)
        {
            foreach (var tag in tags)
            {
                SelectedTags.Remove(tag);
            }
        }
    }
}
