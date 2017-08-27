using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using System;
using System.Linq;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusManager:IBlockDownloadState, INetworkDifficulty
    {
        public ConsensusLoop ConsensusLoop { get; private set; }
        public IDateTimeProvider DateTimeProvider { get; private set; }
        public NodeSettings NodeSettings { get; private set; }
        public Network Network { get; private set; }
        public PowConsensusValidator ConsensusValidator { get; private set; }
        public ChainState ChainState { get; private set; }

        public ConsensusManager(ConsensusLoop consensusLoop = null, IDateTimeProvider dateTimeProvider = null, NodeSettings nodeSettings = null, Network network = null,
            PowConsensusValidator consensusValidator = null, ChainState chainState = null)
        {
            this.ConsensusLoop = consensusLoop;
            this.DateTimeProvider = dateTimeProvider;
            this.NodeSettings = nodeSettings;
            this.Network = network;
            this.ConsensusValidator = consensusValidator;
            this.ChainState = chainState;
        }

        /// <summary>
        /// Checks whether the node is currently in the process of initial block download.
        /// </summary>
        /// <returns><c>true</c> if the node is currently doing IBD, <c>false</c> otherwise.</returns>
        public bool IsInitialBlockDownload()
        {
            if (this.ConsensusLoop == null)
                return false;

            if (this.ConsensusLoop.Tip == null)
                return true;

            if (this.ConsensusLoop.Tip.ChainWork < (this.Network.Consensus.MinimumChainWork ?? uint256.Zero))
                return true;

            if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.NodeSettings.MaxTipAge))
                return true;

            return false;
        }

        public Target GetNetworkDifficulty()
        {
            if (this.ConsensusValidator?.ConsensusParams != null && this.ChainState?.HighestValidatedPoW != null)
                return GetWorkRequired(this.ConsensusValidator.ConsensusParams, this.ChainState?.HighestValidatedPoW);
            else
                return null;
        }

        public static Target GetWorkRequired(NBitcoin.Consensus consensus, ChainedBlock chainedBlock)
        {
            // Genesis block
            if (chainedBlock.Height == 0)
                return consensus.PowLimit;
            var nProofOfWorkLimit = consensus.PowLimit;
            var pindexLast = chainedBlock.Previous;
            var height = chainedBlock.Height;

            if (pindexLast == null)
                return nProofOfWorkLimit;

            // Only change once per interval
            if ((height) % consensus.DifficultyAdjustmentInterval != 0)
            {
                if (consensus.PowAllowMinDifficultyBlocks)
                {
                    // Special difficulty rule for testnet:
                    // If the new block's timestamp is more than 2* 10 minutes
                    // then allow mining of a min-difficulty block.
                    if (chainedBlock.Header.BlockTime > pindexLast.Header.BlockTime + TimeSpan.FromTicks(consensus.PowTargetSpacing.Ticks * 2))
                        return nProofOfWorkLimit;
                    else
                    {
                        // Return the last non-special-min-difficulty-rules-block
                        ChainedBlock pindex = pindexLast;
                        while (pindex.Previous != null && (pindex.Height % consensus.DifficultyAdjustmentInterval) != 0 && pindex.Header.Bits == nProofOfWorkLimit)
                            pindex = pindex.Previous;
                        return pindex.Header.Bits;
                    }
                }
                return pindexLast.Header.Bits;
            }

            // Go back by what we want to be 14 days worth of blocks
            var pastHeight = pindexLast.Height - (consensus.DifficultyAdjustmentInterval - 1);
            ChainedBlock pindexFirst = chainedBlock.EnumerateToGenesis().FirstOrDefault(o => o.Height == pastHeight);
            Guard.Assert(pindexFirst != null);

            if (consensus.PowNoRetargeting)
                return pindexLast.Header.Bits;

            // Limit adjustment step
            var nActualTimespan = pindexLast.Header.BlockTime - pindexFirst.Header.BlockTime;
            if (nActualTimespan < TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4))
                nActualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4);
            if (nActualTimespan > TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4))
                nActualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4);

            // Retarget
            var bnNew = pindexLast.Header.Bits.ToBigInteger();
            bnNew = bnNew.Multiply(BigInteger.ValueOf((long)nActualTimespan.TotalSeconds));
            bnNew = bnNew.Divide(BigInteger.ValueOf((long)consensus.PowTargetTimespan.TotalSeconds));
            var newTarget = new Target(bnNew);
            if (newTarget > nProofOfWorkLimit)
                newTarget = nProofOfWorkLimit;

            return newTarget;
        }
    }
}
