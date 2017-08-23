using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IBlockDownloadState
    {
        bool IsInitialBlockDownload();
    }
}
