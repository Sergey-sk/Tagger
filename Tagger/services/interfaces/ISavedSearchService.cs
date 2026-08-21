using System;
using System.Collections.Generic;
using System.Text;
using Tagger.model;

namespace Tagger.services.interfaces
{
    public interface ISavedSearchService
    {
        Task<List<SavedSearch>> LoadSavedSearchesForPathAsync(string path);
        Task AddSearchAsync(SavedSearch search);
        Task RemoveSearchAsync(SavedSearch search);
    }
}
