using System;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Chain state holds various information related to the status of the chain and its validation.
    /// The data are provided by different components and the chaine state is a mechanism that allows
    /// these components to share that data without creating extra dependencies.
    /// </summary>
    public class ChainState : IBlockDownloadState
    {
        /// <summary>The fullnode interface.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Time until IBD state can be checked.</summary>
        private DateTime lockIbdUntil;

        /// <summary>A cached result of the IBD method.</summary>
        private bool ibdCashed;

        /// <summary>A provider of the date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Store of block header hashes that are to be considered invalid.</summary>
        private readonly IInvalidBlockHashStore invalidBlockHashStore;

        /// <summary>ChainBehaviors sharing this state will not broadcast headers which are above <see cref="ConsensusTip"/>.</summary>
        public ChainedBlock ConsensusTip { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        /// <remarks>TODO: This should be removed once consensus options are part of network.</remarks>
        public uint MaxReorgLength { get; set; }

        /// <summary>
        /// Initialize instance of the object.
        /// </summary>
        /// <param name="fullNode">The full node using this feature.</param>
        /// <param name="invalidBlockHashStore">Store of block header hashes that are to be considered invalid.</param>
        public ChainState(IFullNode fullNode, IInvalidBlockHashStore invalidBlockHashStore)
        {
            this.fullNode = fullNode;
            this.dateTimeProvider = this.fullNode.NodeService<IDateTimeProvider>(true) ?? DateTimeProvider.Default;
            this.invalidBlockHashStore = invalidBlockHashStore;
            this.lockIbdUntil = DateTime.MinValue;
        }

        /// <summary>
        /// Check if a block is marked as invalid.
        /// </summary>
        /// <param name="hashBlock">The block hash to check.</param>
        /// <returns><c>true</c> if the block is marked as invalid.</returns>
        public bool IsMarkedInvalid(uint256 hashBlock)
        {
            return this.invalidBlockHashStore.IsInvalid(hashBlock);
        }

        /// <summary>
        /// Marks a block as invalid. This is used to prevent DOS attacks as the next time the block is seen, it is not processed again.
        /// </summary>
        /// <param name="hashBlock">The block hash to mark as invalid.</param>
        /// <param name="rejectedUntil">Time in UTC after which the block is no longer considered as invalid, or <c>null</c> if the block is to be considered invalid forever.</param>
        public void MarkBlockInvalid(uint256 hashBlock, DateTime? rejectedUntil = null)
        {
            this.invalidBlockHashStore.MarkInvalid(hashBlock, rejectedUntil);
        }

        /// <summary>
        /// This method will check if the node is in a state of IBD (Initial Block Download).
        /// </summary>
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
