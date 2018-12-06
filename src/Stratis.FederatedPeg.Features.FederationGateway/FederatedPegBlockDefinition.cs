using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using Script = NBitcoin.Script;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class FederatedPegBlockDefinition : PoABlockDefinition
    {
        private readonly Script payToMultisigScript;

        private readonly Script payToMemberScript;

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
            this.payToMemberScript = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(new PubKey(federationGatewaySettings.PublicKey));
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            var rewardScript = chainTip.Height == this.Network.Consensus.PremineHeight 
                                   ? this.payToMultisigScript 
                                   : this.payToMemberScript;

            base.OnBuild(chainTip, this.payToMultisigScript);

            return this.BlockTemplate;
        }

    }
}