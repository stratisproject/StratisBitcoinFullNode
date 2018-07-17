using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NBitcoin;

namespace Stratis.Bitcoin.Tests.Common
{
    public static class ChainedHeadersHelper
    {
        private static int currentNonce = 0;

        public static List<ChainedHeader> CreateConsequtiveHeaders(int count, ChainedHeader prevBlock = null)
        {
            var chainedHeaders = new List<ChainedHeader>();
            Network network = Network.StratisMain;

            ChainedHeader tip = prevBlock ?? CreateGenesisChainedHeader();
            uint256 hashPrevBlock = tip.HashBlock;

            for (int i = 0; i < count; ++i)
            {
                BlockHeader header = network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.Nonce = (uint)Interlocked.Increment(ref currentNonce);
                header.HashPrevBlock = hashPrevBlock;
                header.Bits = Target.Difficulty1;

                var chainedHeader = new ChainedHeader(header, header.GetHash(), tip);

                hashPrevBlock = chainedHeader.HashBlock;
                tip = chainedHeader;

                chainedHeaders.Add(chainedHeader);
            }

            return chainedHeaders;
        }

        public static ChainedHeader CreateGenesisChainedHeader()
        {
            return new ChainedHeader(Network.StratisMain.GetGenesis().Header, Network.StratisMain.GenesisHash, 0);
        }
    }
}
