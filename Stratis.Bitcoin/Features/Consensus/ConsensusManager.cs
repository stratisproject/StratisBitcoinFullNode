﻿using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;

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
                return Miner.PowMining.GetWorkRequired(this.ConsensusValidator.ConsensusParams, this.ChainState?.HighestValidatedPoW);
            else
                return null;
        }
    }
}
