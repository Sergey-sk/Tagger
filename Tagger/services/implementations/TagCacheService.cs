using System;
using System.Collections.Generic;
using System.Text;
using Tagger.services.interfaces;
using Tagger.viewmodel;

namespace Tagger.services.implementations
{
    public class TagCacheService : ITagCacheService
    {
        private readonly ITagService _tagService;
        private Dictionary<int, TagItemViewModel> _cachedUiTags = [];

        public TagCacheService(ITagService tagService) => _tagService = tagService;

        public async Task<Dictionary<int, TagItemViewModel>> GetOrCreateCacheAsync()
        {
            if (_cachedUiTags.Count > 0) return _cachedUiTags;

            var dbTags = await _tagService.LoadTagsAsync();

            _cachedUiTags = dbTags.ToDictionary(t => t.Id, t => new TagItemViewModel(t, () => []));
            return _cachedUiTags;
        }

        public async Task<List<TagItemViewModel>> GetSelectedTags()
        {
            var tags = await GetOrCreateCacheAsync();
            return [.. tags.Where(pair => pair.Value.IsSelected).Select(pair => pair.Value)];
        }

        public void Remove(int tagId) => _cachedUiTags.Remove(tagId);
    }
}
