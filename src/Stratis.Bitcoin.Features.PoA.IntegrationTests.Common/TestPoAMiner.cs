using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        private CancellationTokenSource cancellation;

        private readonly SlotsManager slotsManager;

        private readonly IConsensusManager consensusManager;

        private readonly FederationManager federationManager;

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
            VotingManager votingManager,
            PoAMinerSettings poAMinerSettings) : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager,
                connectionManager, poaHeaderValidator, federationManager, integrityValidator, walletManager, nodeStats, votingManager, poAMinerSettings)
        {
            this.timeProvider = dateTimeProvider as EditableTimeProvider;

            this.cancellation = new CancellationTokenSource();
            this.slotsManager = slotsManager;
            this.consensusManager = consensusManager;
            this.federationManager = federationManager;
        }

        public override void InitializeMining()
        {
        }

        public async Task MineBlocksAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                this.timeProvider.AdjustedTimeOffset += TimeSpan.FromSeconds(
                    this.slotsManager.GetRoundLengthSeconds(this.federationManager.GetFederationMembers().Count));

                uint timeNow = (uint)this.timeProvider.GetAdjustedTimeAsUnixTimestamp();

                uint myTimestamp = this.slotsManager.GetMiningTimestamp(timeNow);

                this.timeProvider.AdjustedTimeOffset += TimeSpan.FromSeconds(myTimestamp - timeNow);

                ChainedHeader chainedHeader = await this.MineBlockAtTimestampAsync(myTimestamp).ConfigureAwait(false);

                if (chainedHeader == null)
                {
                    i--;
                    this.timeProvider.AdjustedTimeOffset += TimeSpan.FromHours(1);
                    continue;
                }

                var builder = new StringBuilder();
                builder.AppendLine("<<==============================================================>>");
                builder.AppendLine($"Block was mined {chainedHeader}.");
                builder.AppendLine("<<==============================================================>>");
                this.logger.LogInformation(builder.ToString());

            }
        }

        public override void Dispose()
        {
            this.cancellation.Cancel();
            base.Dispose();
        }
    }
}
