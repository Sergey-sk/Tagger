namespace Tagger.services.interfaces
{
    public interface ITagCollectionController
    {
        void AppendFilesToUiTags(List<int> tagIds, int filesCount);
    }
}
