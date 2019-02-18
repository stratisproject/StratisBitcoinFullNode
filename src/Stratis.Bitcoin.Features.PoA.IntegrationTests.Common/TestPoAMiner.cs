using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Common;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Common
{
    public class TestPoAMiner : PoAMiner
    {
        private readonly EditableTimeProvider timeProvider;

        private AsyncQueue<uint> timestampQueue;

        private CancellationTokenSource cancellation;

        private readonly SlotsManager slotsManager;

        private readonly IConsensusManager consensusManager;

        public TestPoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState,
            BlockDefinition blockDefinition,
            SlotsManager slotsManager,
            IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator,
            FederationManager federationManager,
            IIntegrityValidator integrityValidator,
            IWalletManager walletManager,
            INodeStats nodeStats,
            VotingManager votingManager) : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager,
                connectionManager, poaHeaderValidator, federationManager, integrityValidator, walletManager, nodeStats, votingManager)
        {
            this.timeProvider = dateTimeProvider as EditableTimeProvider;

            this.timestampQueue = new AsyncQueue<uint>();
            this.cancellation = new CancellationTokenSource();
            this.slotsManager = slotsManager;
            this.consensusManager = consensusManager;
        }

        protected override async Task<uint> WaitUntilMiningSlotAsync()
        {
            uint nextTimestamp = await this.timestampQueue.DequeueAsync(this.cancellation.Token).ConfigureAwait(false);

            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(nextTimestamp);

            this.timeProvider.AdjustedTimeOffset += dateTime.TimeOfDay;

            return nextTimestamp;
        }

        public async Task MineBlocksAsync(int count)
        {
            int nextHeight = this.consensusManager.Tip.Height;

            for (int i = 0; i < count; i++)
            {
                nextHeight++;

                uint timeNow = (uint)this.timeProvider.GetAdjustedTimeAsUnixTimestamp();

                uint myTimestamp = this.slotsManager.GetMiningTimestamp(timeNow);

                this.timestampQueue.Enqueue(myTimestamp);

                TestHelper.WaitLoop(() => this.consensusManager.Tip.Height >= nextHeight);
            }
        }

        public override void Dispose()
        {
            this.cancellation.Cancel();
            base.Dispose();
        }
    }
}
