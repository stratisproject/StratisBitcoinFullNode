using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class TestConsensusManager : ConsensusManager
    {
        public TestConsensusManager(
            IChainedHeaderTree chainedHeaderTree,
            Network network,
            ILoggerFactory loggerFactory,
            IChainState chainState,
            IIntegrityValidator integrityValidator,
            IPartialValidator partialValidator,
            IFullValidator fullValidator,
            IConsensusRuleEngine consensusRules,
            IFinalizedBlockInfoRepository finalizedBlockInfo,
            ISignals signals,
            IPeerBanning peerBanning,
            IInitialBlockDownloadState ibdState,
            ConcurrentChain chain,
            IBlockPuller blockPuller,
            IBlockStore blockStore,
            IConnectionManager connectionManager,
            INodeStats nodeStats,
            INodeLifetime nodeLifetime) : base 
            (chainedHeaderTree, network, loggerFactory, chainState, integrityValidator, partialValidator, fullValidator, 
                consensusRules, finalizedBlockInfo, signals, peerBanning, ibdState, chain, blockPuller, blockStore, connectionManager, nodeStats, nodeLifetime)
        {

        }


        public bool PeerIsKnown(int peerId)
        {
            return base.PeersByPeerId.ContainsKey(peerId);
        }


        public long GetExpectedBlockDataBytes()
        {
            return base.ExpectedBlockDataBytes;
        }

        public Dictionary<uint256, long> GetExpectedBlockSizes()
        {
            return base.ExpectedBlockSizes;
        }

        public void SetMaxUnconsumedBlocksDataBytes(long newSize)
        {
            base.MaxUnconsumedBlocksDataBytes = newSize;
        }


    }
}
