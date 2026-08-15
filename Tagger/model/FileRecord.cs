namespace Tagger.model
{
    public class FileRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsDeleted { get; set; }

        public List<Tag> Tags { get; set; } = new();
    }
}
