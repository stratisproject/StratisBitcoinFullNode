using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class IBDStateProvider : IBlockDownloadState
    {
        /// <summary>Provider of block header hash checkpoints.</summary>
        private readonly ICheckpoints checkpoints;

        /// <summary>A provider of the date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        private readonly ChainState chainState;

        private readonly Network network;

        private readonly NodeSettings nodeSettings;

        /// <summary>Time until IBD state can be checked.</summary>
        private DateTime lockIbdUntil;

        /// <summary>A cached result of the IBD method.</summary>
        private bool ibdCashed;

        public IBDStateProvider(ChainState chainState, Network network, NodeSettings nodeSettings, DateTimeProvider dateTimeProvider, ICheckpoints checkpoints)
        {
            this.network = network;
            this.nodeSettings = nodeSettings;
            this.chainState = chainState;
            this.checkpoints = checkpoints;
            this.dateTimeProvider = dateTimeProvider;

            this.lockIbdUntil = DateTime.MinValue;
        }

        /// <summary>
        /// Checks whether the node is currently in the process of initial block download.
        /// </summary>
        /// <returns><c>true</c> if the node is currently doing IBD, <c>false</c> otherwise.</returns>
        public bool IsInitialBlockDownload()
        {
            if (this.lockIbdUntil >= this.dateTimeProvider.GetUtcNow())
                return this.ibdCashed;

            if (this.chainState == null)
                return false;

            if (this.chainState.ConsensusTip == null)
                return true;

            if (this.checkpoints.GetLastCheckpointHeight() > this.chainState.ConsensusTip.Height)
                return true;

            if (this.chainState.ConsensusTip.ChainWork < (this.network.Consensus.MinimumChainWork ?? uint256.Zero))
                return true;

            if (this.chainState.ConsensusTip.Header.BlockTime.ToUnixTimeSeconds() < (this.dateTimeProvider.GetTime() - this.nodeSettings.MaxTipAge))
                return true;

            return false;
        }

        /// <summary>
        /// Sets last IBD status update time and result.
        /// <para>Used in tests only.</para>
        /// </summary>
        /// <param name="val">New value for the IBD status, <c>true</c> means the node is considered in IBD.</param>
        /// <param name="time">Time until IBD state can be checked.</param>
        public void SetIsInitialBlockDownload(bool val, DateTime time)
        {
            this.lockIbdUntil = time;
            this.ibdCashed = val;
        }
    }
}
