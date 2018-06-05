using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    public interface ISidechainInfoProvider
    {
        SidechainInfo GetSidechainInfo(string sidechainName);
        Task<SidechainInfo> GetSidechainInfoAsync(string sidechainName);
        void VerifyFolder(string filename);
    }
}
