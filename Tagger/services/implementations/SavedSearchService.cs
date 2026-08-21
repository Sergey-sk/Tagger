using Microsoft.EntityFrameworkCore;
using Tagger.model;
using Tagger.services.interfaces;

namespace Tagger.services.implementations
{
    public class SavedSearchService : ISavedSearchService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public SavedSearchService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<SavedSearch>> LoadSavedSearchesForPathAsync(string path)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.SavedSearches
                .AsNoTracking()
                .Where(s => s.FolderPath == path)
                .ToListAsync();
        }

        public async Task AddSearchAsync(SavedSearch search)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await context.SavedSearches.AddAsync(search);
            await context.SaveChangesAsync();
        }

        public async Task RemoveSearchAsync(SavedSearch search)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            context.SavedSearches.Remove(search);
            await context.SaveChangesAsync();
        }
    }
}
