using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    public interface ISidechainsManager
    {
        Task<Dictionary<string, SidechainInfo>> ListSidechains();
        Task NewSidechain(SidechainInfoRequest sidechainInfoRequest);
        Task<CoinDetails> GetCoinDetails();
    }
}