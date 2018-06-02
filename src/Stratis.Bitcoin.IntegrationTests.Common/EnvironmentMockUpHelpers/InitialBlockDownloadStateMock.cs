using System;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class InitialBlockDownloadStateMock : IInitialBlockDownloadState
    {
        /// <summary>Time until IBD state can be checked.</summary>
        private DateTime lockIbdUntil;

        /// <summary>A cached result of the IBD method.</summary>
        private bool blockDownloadState;

        /// <summary>A provider of the date and time.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        private readonly InitialBlockDownloadState innerBlockDownloadState;

        public InitialBlockDownloadStateMock(IChainState chainState, Network network, NodeSettings nodeSettings,
            ICheckpoints checkpoints)
        {
            this.lockIbdUntil = DateTime.MinValue;
            this.dateTimeProvider = DateTimeProvider.Default;

            this.innerBlockDownloadState = new InitialBlockDownloadState(chainState, network, nodeSettings, checkpoints);
        }

        public bool IsInitialBlockDownload()
        {
            if (this.lockIbdUntil >= this.dateTimeProvider.GetUtcNow())
                return this.blockDownloadState;

            return this.innerBlockDownloadState.IsInitialBlockDownload();
        }

        /// <summary>
        /// Sets last IBD status update time and result.
        /// </summary>
        /// <param name="blockDownloadState">New value for the IBD status, <c>true</c> means the node is considered in IBD.</param>
        /// <param name="lockIbdUntil">Time until IBD state won't be changed.</param>
        public void SetIsInitialBlockDownload(bool blockDownloadState, DateTime lockIbdUntil)
        {
            this.lockIbdUntil = lockIbdUntil;
            this.blockDownloadState = blockDownloadState;
        }
    }
}
