using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Common
{
    public class TestPoAMiner : PoAMiner
    {
        public bool FastMiningEnabled { get; private set; } = false;

        private readonly EditableTimeProvider timeProvider;

        private CancellationTokenSource cancellationSource;

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
            INodeStats nodeStats) : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager,
                connectionManager, poaHeaderValidator, federationManager, integrityValidator, walletManager, nodeStats)
        {
            this.timeProvider = dateTimeProvider as EditableTimeProvider;
            this.cancellationSource = new CancellationTokenSource();
        }

        public void EnableFastMining()
        {
            this.FastMiningEnabled = true;
            this.cancellationSource.Cancel();
        }

        public void DisableFastMining()
        {
            this.FastMiningEnabled = false;
            this.cancellationSource = new CancellationTokenSource();
        }

        protected override async Task TaskDelayAsync(int delayMs, CancellationToken cancellation = default(CancellationToken))
        {
            if (this.FastMiningEnabled)
            {
                this.timeProvider.AdjustedTimeOffset += TimeSpan.FromMilliseconds(delayMs);
            }
            else
            {
                try
                {
                    CancellationToken token = CancellationTokenSource.CreateLinkedTokenSource(this.cancellationSource.Token, cancellation).Token;

                    await base.TaskDelayAsync(delayMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }
}
