using CommunityToolkit.Mvvm.ComponentModel;

namespace Tagger.model
{
    public partial class Tag:ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public List<FileRecord> Files { get; set; } = new();

        [ObservableProperty]
        private bool _isSelected;
    }
}
