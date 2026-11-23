using Tagger.services.interfaces;
using Tagger.viewmodel;

namespace Tagger.services.implementations
{
    public class TagCacheService : ITagCacheService
    {
        private readonly ITagService _tagService;
        private Dictionary<int, TagItemViewModel> _cachedUiTags = [];
        private List<TagItemViewModel> _tagsForSelection = [];

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

        public async Task<Dictionary<int, TagItemViewModel>> UpdateAsync()
        {
            _cachedUiTags.Clear();
            return await GetOrCreateCacheAsync();
        }

        public List<TagItemViewModel> GetTagsForSelection()
        {
            return _tagsForSelection;
        }

        public void AddTagForSelection(TagItemViewModel tag)
        {
            if (_tagsForSelection.Any(t => t.Id == tag.Id)) return;
            _tagsForSelection.Add(tag);
        }

        public void RemoveTagForSelection(int id)
        {
            _tagsForSelection.RemoveAll(t => t.Id == id);
        }

        public void RemoveAllTagForSelection() => _tagsForSelection.Clear();
    }
}
