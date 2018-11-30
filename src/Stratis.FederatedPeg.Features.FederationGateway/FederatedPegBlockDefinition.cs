using System;

using Microsoft.Extensions.Logging;

using NBitcoin;

using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class FederatedPegBlockDefinition : PoABlockDefinition
    {
        private readonly Script payToMultisigScript;

        /// <inheritdoc />
        public FederatedPegBlockDefinition(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            IFederationGatewaySettings federationGatewaySettings)
            : base(consensusManager, dateTimeProvider, loggerFactory, mempool, mempoolLock, network)
        {
            this.payToMultisigScript = federationGatewaySettings.MultiSigAddress.ScriptPubKey;
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            base.OnBuild(chainTip, this.payToMultisigScript);

            return this.BlockTemplate;
        }

    }
}