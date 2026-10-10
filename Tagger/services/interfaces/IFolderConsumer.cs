using System;
using System.Collections.Generic;
using System.Text;

namespace Tagger.services.interfaces
{
    public interface IFolderConsumer
    {
        ValueTask OnFolderChanged(string path);
    }
}
