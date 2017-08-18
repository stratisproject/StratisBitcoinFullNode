using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin
{
    public interface IConsensusFeature
    {
        bool IsInitialBlockDownload();
    }
}
