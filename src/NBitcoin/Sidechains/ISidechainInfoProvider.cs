using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin
{
    public interface ISidechainInfoProvider
    {
        SidechainInfo GetSidechainInfo(string sidechainName);
        void VerifyFolder(string filename);
    }
}
