using System.Collections.Generic;
using System.Threading;
using NBitcoin;

namespace Stratis.Bitcoin.Tests.Common
{
    public static class ChainedHeadersHelper
    {
        private static int currentNonce = 0;

        /// <summary>
        /// Creates specified amount of consecutive headers.
        /// </summary>
        /// <param name="count">Amount of blocks to generate.</param>
        /// <param name="prevBlock">If not <c>null</c> the headers will be generated on top of it.</param>
        /// <returns></returns>
        public static List<ChainedHeader> CreateConsecutiveHeaders(int count, ChainedHeader prevBlock = null)
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

        /// <summary>Creates genesis header for stratis mainnet.</summary>
        public static ChainedHeader CreateGenesisChainedHeader()
        {
            return new ChainedHeader(Network.StratisMain.GetGenesis().Header, Network.StratisMain.GenesisHash, 0);
        }
    }
}
