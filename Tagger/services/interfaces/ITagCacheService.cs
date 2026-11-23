using System;
using System.Collections.Generic;
using System.Text;
using Tagger.viewmodel;

namespace Tagger.services.interfaces
{
    public interface ITagCacheService
    {
        Task<Dictionary<int, TagItemViewModel>> GetOrCreateCacheAsync();
        Task<List<TagItemViewModel>> GetSelectedTags();
        void Remove(int tagId);
        Task<Dictionary<int, TagItemViewModel>> UpdateAsync();
        List<TagItemViewModel> GetTagsForSelection();
        void AddTagForSelection(TagItemViewModel tag);
        void RemoveTagForSelection(int id);
        void RemoveAllTagForSelection();
    }
}
