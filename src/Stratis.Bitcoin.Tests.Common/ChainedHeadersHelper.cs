﻿using System.Collections.Generic;
using System.Threading;
using NBitcoin;

namespace Stratis.Bitcoin.Tests.Common
{
    public static class ChainedHeadersHelper
    {
        /// <summary>
        /// Nonce that will be used in header creation.
        /// Every header should have a different nonce to avoid possibility of creating headers with same hash.
        /// </summary>
        private static int currentNonce = 0;

        /// <summary>Creates specified number of consecutive headers.</summary>
        /// <param name="count">Number of blocks to generate.</param>
        /// <param name="prevBlock">If not <c>null</c> the headers will be generated on top of it.</param>
        /// <param name="includePrevBlock">If <c>true</c> <paramref name="prevBlock"/> will be added as a first item in the list or genesis header if <paramref name="prevBlock"/> is <c>null</c>.</param>
        public static List<ChainedHeader> CreateConsecutiveHeaders(int count, ChainedHeader prevBlock = null, bool includePrevBlock = false)
        {
            var chainedHeaders = new List<ChainedHeader>();
            Network network = Networks.StratisMain;

            ChainedHeader tip = prevBlock;

            if (tip == null)
            {
                ChainedHeader genesis = CreateGenesisChainedHeader();
                tip = genesis;
            }

            if (includePrevBlock)
                chainedHeaders.Add(tip);

            uint256 hashPrevBlock = tip.HashBlock;

            for (int i = 0; i < count; i++)
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
            return new ChainedHeader(Networks.StratisMain.GetGenesis().Header, Networks.StratisMain.GenesisHash, 0);
        }
    }
}
