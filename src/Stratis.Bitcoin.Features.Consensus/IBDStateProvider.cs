using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public interface IBlockDownloadState
    {
        bool IsInitialBlockDownload();
    }

    public class IBDStateProvider : IBlockDownloadState
    {
        /// <summary>Provider of block header hash checkpoints.</summary>
        private readonly ICheckpoints checkpoints;

        /// <summary>A provider of the date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Time until IBD state can be checked.</summary>
        private DateTime lockIbdUntil;

        /// <summary>A cached result of the IBD method.</summary>
        private bool ibdCashed;

        public IBDStateProvider()
        {
            //TODO
            //dateTimeProvider =

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

            /*
            if (this.ConsensusLoop == null)
                return false;

            if (this.ConsensusLoop.Tip == null)
                return true;

            if (this.checkpoints.GetLastCheckpointHeight() > this.ConsensusLoop.Tip.Height)
                return true;

            if (this.ConsensusLoop.Tip.ChainWork < (this.Network.Consensus.MinimumChainWork ?? uint256.Zero))
                return true;

            if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.NodeSettings.MaxTipAge))
                return true;
            */

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
