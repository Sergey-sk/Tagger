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

        public long LastScanId { get; set; }
        public int SyncStatus { get; set; }
        public long? PendingSize { get; set; }
        public DateTime? PendingLastModified { get; set; }

        public List<Tag> Tags { get; set; } = new();
    }
}
