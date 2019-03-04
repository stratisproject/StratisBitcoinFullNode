using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
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

namespace Stratis.Features.FederatedPeg
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
            NodeSettings nodeSettings)
            : base(blockBufferGenerator, coinView, consensusManager, dateTimeProvider, executorFactory, loggerFactory, mempool, mempoolLock, network, senderRetriever, stateRoot, nodeSettings)
        {
            var federationGatewaySettings = new FederationGatewaySettings(nodeSettings);
            this.payToMultisigScript = federationGatewaySettings.MultiSigAddress.ScriptPubKey;
            this.payToMemberScript = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(new PubKey(federationGatewaySettings.PublicKey));
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            Script rewardScript = (chainTip.Height + 1) == this.Network.Consensus.PremineHeight 
                                   ? this.payToMultisigScript 
                                   : this.payToMemberScript;

            return base.Build(chainTip, rewardScript);
        }
    }
}