using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public class SmartContractPoARuleEngine: PoAConsensusRuleEngine, ISmartContractCoinviewRule
    {
        public ICallDataSerializer CallDataSerializer { get; private set; }
        public IContractExecutorFactory ExecutorFactory { get; private set; }
        public IStateRepositoryRoot OriginalStateRoot { get; private set; }
        public IReceiptRepository ReceiptRepository { get; private set; }
        public ISenderRetriever SenderRetriever { get; private set; }

        public SmartContractPoARuleEngine(
            ICallDataSerializer callDataSerializer,
            ConcurrentChain chain,
            ICheckpoints checkpoints,
            ConsensusSettings consensusSettings,
            IDateTimeProvider dateTimeProvider,
            IContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            Network network,
            NodeDeployments nodeDeployments,
            IStateRepositoryRoot originalStateRoot,
            IReceiptRepository receiptRepository,
            ISenderRetriever senderRetriever,
            ICoinView utxoSet,
            IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore,
            INodeStats nodeStats,
            SlotsManager slotsManager,
            PoABlockHeaderValidator poaHeaderValidator,
            VotingManager votingManager,
            FederationManager federationManager)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState,
                invalidBlockHashStore, nodeStats, slotsManager, poaHeaderValidator, votingManager, federationManager)
        {
            this.CallDataSerializer = callDataSerializer;
            this.ExecutorFactory = executorFactory;
            this.OriginalStateRoot = originalStateRoot;
            this.ReceiptRepository = receiptRepository;
            this.SenderRetriever = senderRetriever;
        }
    }
}
