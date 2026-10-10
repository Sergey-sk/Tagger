using Microsoft.EntityFrameworkCore;
using Tagger.db;
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
                .Where(f => f.Path.StartsWith(path) && !f.IsDeleted)
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
            string path,
            HashSet<int> tagIds,
            CancellationToken token)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Files
                .AsNoTracking()
                .Include(f => f.Tags)
                .Where(f => f.Path.StartsWith(path));

            if (tagIds.Count > 0)
            {
                query = query.Where(f => f.Tags.Any(t => tagIds.Contains(t.Id)));
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string[] keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyword in keywords)
                {
                    string pattern = $"%{keyword}%";
                    query = query.Where(f => EF.Functions.Like(f.Name, pattern));
                }
            }

            token.ThrowIfCancellationRequested();

            var result = await query.Take(3000).ToListAsync(token);

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

        public async Task DetachTagFromFilesAsync(int tagId, List<int> selectedFileIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.FileTags
                .Where(ft => selectedFileIds.Contains(ft.FileId) && tagId == ft.TagId)
                .ExecuteDeleteAsync();
        }
    }
}
