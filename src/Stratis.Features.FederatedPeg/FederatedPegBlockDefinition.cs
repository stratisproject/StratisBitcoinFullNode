using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
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
        /// <summary>
        /// The number of outputs we break the premine reward up into, so that the federation can build more than one transaction at once.
        /// </summary>
        public const int FederationWalletOutputs = 10;

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
            NodeSettings nodeSettings,
            MinerSettings minerSettings)
            : base(blockBufferGenerator, coinView, consensusManager, dateTimeProvider, executorFactory, loggerFactory, mempool, mempoolLock, network, senderRetriever, stateRoot, minerSettings)
        {
            var federationGatewaySettings = new FederationGatewaySettings(nodeSettings);
            this.payToMultisigScript = federationGatewaySettings.MultiSigAddress.ScriptPubKey;
            this.payToMemberScript = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(new PubKey(federationGatewaySettings.PublicKey));
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            // TODO: Shouldn't this be mining to a local wallet. Use this variable ^^

            bool miningPremine = (chainTip.Height + 1) == this.Network.Consensus.PremineHeight;

            Script rewardScript = miningPremine ? this.payToMultisigScript : this.payToMemberScript;

            BlockTemplate built = base.Build(chainTip, rewardScript);

            if (miningPremine)
            {
                this.BreakUpPremineCoinbase();
            }

            return built;
        }

        private void BreakUpPremineCoinbase()
        {
            TxOut premineOutput = this.coinbase.Outputs[0];

            Money newTxOutValues = premineOutput.Value / FederationWalletOutputs;
            Script newTxOutScript = premineOutput.ScriptPubKey;

            this.coinbase.Outputs.Clear();

            for (int i = 0; i < FederationWalletOutputs; i++)
            {
                this.coinbase.AddOutput(newTxOutValues, newTxOutScript);
            }
        }

    }
}