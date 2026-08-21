namespace Tagger.services.Drag_Drop
{
    public class DraggedObjectsPackage<T>
    {
        public int Count { get; set; }
        public List<T> Objects { get; set; }
    }
}
