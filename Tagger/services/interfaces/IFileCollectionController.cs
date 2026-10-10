using System;
using System.Collections.Generic;
using System.Text;
using Tagger.model;
using Tagger.viewmodel;

namespace Tagger.services.interfaces
{
    public interface IFileCollectionController
    {
        void AppendTagsToUiFiles(List<int> fileIds, List<TagItemViewModel> uiTags, List<Tag> dbTags);
    }
}
