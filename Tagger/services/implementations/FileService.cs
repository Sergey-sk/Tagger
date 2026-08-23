using Microsoft.EntityFrameworkCore;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class FileService : IFileService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public FileService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<FileRecord>> LoadFirstBatchFilesAsync(string path, CancellationToken token)
        {
            using var context = await _contextFactory.CreateDbContextAsync(token);

            return await context.Files
                .AsNoTracking()
                .Include(f => f.Tags)
                .Where(f => f.Path.StartsWith(path))
                .Take(3000)
                .ToListAsync(token);
        }

        public async Task<List<FileRecord>> LoadFilesForPathInBackgroundAsync(string path, CancellationToken token)
        {
            using var bgContext = await _contextFactory.CreateDbContextAsync();

            return await bgContext.Files
                .AsNoTracking()
                .Include(f => f.Tags)
                .Where(f => f.Path.StartsWith(path))
                .ToListAsync(token);
        }

        public async Task<List<FileRecord>> SearchWithDebounceAsync(
            string searchText,
            List<FileRecord> files,
            string path,
            List<int> tagIds,
            CancellationTokenSource? searchCts)
        {
            searchCts?.Cancel();
            searchCts = new CancellationTokenSource();
            var token = searchCts.Token;

            if (!string.IsNullOrEmpty(searchText))
                await Task.Delay(150, token);

            if (files.Count == 0) return [];

            IEnumerable<FileRecord> query = files
                .Where(f => f.Path.StartsWith(path));

            if (tagIds.Count > 0)
            {
                foreach (var tagId in tagIds)
                {
                    query = query.Where(f => f.Tags.Any(t => t.Id == tagId));
                }
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string[] keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyword in keywords)
                {
                    query = query.Where(f => f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }
            }

            token.ThrowIfCancellationRequested();

            var result = query.Take(3000).ToList();

            token.ThrowIfCancellationRequested();

            return result;
        }

        public async Task<List<Tag>> GetTagsByIds(List<int> tagIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var setIds = tagIds.ToHashSet();

            return await context.Tags
                .Where(t => setIds.Contains(t.Id))
                .ToListAsync();
        }

        public async Task<Tag?> GetTagByIdAsync(int tagId)
        {
            var tags = await GetTagsByIds([tagId]);
            return tags.FirstOrDefault();
        }
    }
}
