namespace Tagger.services.interfaces
{
    public interface IScanningService
    {
        Task ScanDirectoryAsync(string path,
            string lastSuccessScanTime,
            IProgress<int>? progress,
            CancellationToken cancellationToken,
            bool isBackground = false);

        Task ApplyBackgroundScanChangesAsync(string rootPath, CancellationToken cancellationToken);

        Task DiscardBackgroundScanChangesAsync(string rootPath, CancellationToken cancellationToken);
    }
}
