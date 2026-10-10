namespace Tagger.model
{
    public partial class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public List<FileRecord> Files { get; set; } = new();
        public int ActiveFilesCount => Files.Count(f => !f.IsDeleted);
    }
}
