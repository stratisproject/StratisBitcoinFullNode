using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Script = NBitcoin.Script;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class FederatedPegBlockDefinition : SmartContractPoABlockDefinition
    {
        private readonly Script payToMultisigScript;

        private readonly Script payToMemberScript;

        /// <inheritdoc />
        public FederatedPegBlockDefinition(
            IBlockBufferGenerator blockBufferGenerator,
            ICoinView coinView,
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            IContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            ISenderRetriever senderRetriever,
            IStateRepositoryRoot stateRoot,
            IFederationGatewaySettings federationGatewaySettings)
            : base(blockBufferGenerator, coinView, consensusManager, dateTimeProvider, executorFactory, loggerFactory, mempool, mempoolLock, network, senderRetriever, stateRoot)
        {
            this.payToMultisigScript = federationGatewaySettings.MultiSigAddress.ScriptPubKey;
            this.payToMemberScript = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(new PubKey(federationGatewaySettings.PublicKey));
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            var rewardScript = chainTip.Height == this.Network.Consensus.PremineHeight 
                                   ? this.payToMultisigScript 
                                   : this.payToMemberScript;

            base.OnBuild(chainTip, rewardScript);

            return this.BlockTemplate;
        }

    }
}