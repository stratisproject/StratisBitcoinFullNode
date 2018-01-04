using System;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public class InitialBlockDownloadStateMock : InitialBlockDownloadState
    {
        /// <summary>Time until IBD state can be checked.</summary>
        private DateTime lockIbdUntil;

        /// <summary>A cached result of the IBD method.</summary>
        private bool blockDownloadState;

        public InitialBlockDownloadStateMock(ChainState chainState, Network network, NodeSettings nodeSettings,
            ICheckpoints checkpoints) : base (chainState, network, nodeSettings, checkpoints)
        {
            this.lockIbdUntil = DateTime.MinValue;
        }

        public override bool IsInitialBlockDownload()
        {
            if (this.lockIbdUntil >= this.dateTimeProvider.GetUtcNow())
                return this.blockDownloadState;

            return base.IsInitialBlockDownload();
        }

        /// <summary>
        /// Sets last IBD status update time and result.
        /// <para>Used in tests only.</para>
        /// </summary>
        /// <param name="blockDownloadState">New value for the IBD status, <c>true</c> means the node is considered in IBD.</param>
        /// <param name="lockStateUntil">Time until IBD state won't be changed.</param>
        public void SetIsInitialBlockDownload(bool blockDownloadState, DateTime lockStateUntil)
        {
            this.lockIbdUntil = lockStateUntil;
            this.blockDownloadState = blockDownloadState;
        }
    }
}
