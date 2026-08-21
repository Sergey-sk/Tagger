using System;
using System.Collections.Generic;
using System.Text;

namespace Tagger.services.interfaces
{
    public interface IFileTagUpdater
    {
        Task OnApplyObject(int id, List<int> objToApplyIds);
        Task ApplyTagToFileAsync(int id, List<int> objToApplyIds);
        void UpdateUI();
    }
}
