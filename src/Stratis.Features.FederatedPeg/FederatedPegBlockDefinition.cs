using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.SmartContracts.CLR;
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

        private readonly ICoinbaseSplitter premineSplitter;

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
            ICoinbaseSplitter premineSplitter,
            IBlockExecutionResultCache executionCache,
            ICallDataSerializer callDataSerializer,
            MinerSettings minerSettings,
            FederatedPegSettings federatedPegSettings,
            NodeDeployments nodeDeployments)
            : base(blockBufferGenerator, coinView, consensusManager, dateTimeProvider, executorFactory, loggerFactory, mempool, mempoolLock, network, senderRetriever, stateRoot, executionCache, callDataSerializer, minerSettings, nodeDeployments)
        {
            this.payToMultisigScript = federatedPegSettings.MultiSigAddress.ScriptPubKey;

            this.premineSplitter = premineSplitter;
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            // Note: When creating a new chain, ensure that the first nodes mining are the federated peg nodes, 
            // so that the premine goes to the federated peg wallet.

            // The other nodes don't know about the federated wallet in the current design.
            // If this changes, a consensus rule should be built that enforces that the premine goes to that address.

            bool miningPremine = (chainTip.Height + 1) == this.Network.Consensus.PremineHeight;

            // If we are not mining the premine, then the reward should fall back to what was selected by the caller.
            Script rewardScript = miningPremine ? this.payToMultisigScript : scriptPubKey;

            BlockTemplate built = base.Build(chainTip, rewardScript);

            if (miningPremine)
            {
                this.premineSplitter.SplitReward(this.coinbase);
            }

            return built;
        }

    }
}