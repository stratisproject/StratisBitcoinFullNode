using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA.Rules
{
    public class SmartContractPoACoinviewRule : PoACoinviewRule
    {
        private SmartContractCoinViewRuleLogic logic;
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;
        private readonly List<Transaction> blockTxsProcessed;
        private Transaction generatedTransaction;
        private readonly IList<Receipt> receipts;
        private uint refundCounter;
        private IStateRepositoryRoot mutableStateRepository;

        public SmartContractPoACoinviewRule(
            IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever,
            IReceiptRepository receiptRepository,
            ICoinView coinView)
        {
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.executorFactory = executorFactory;
            this.callDataSerializer = callDataSerializer;
            this.senderRetriever = senderRetriever;
            this.receiptRepository = receiptRepository;
            this.coinView = coinView;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            this.logic = new SmartContractCoinViewRuleLogic(this.stateRepositoryRoot, this.executorFactory, this.callDataSerializer, this.senderRetriever, this.receiptRepository, this.coinView);
        }

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            await this.logic.RunAsync(base.RunAsync, context);
        }

        /// <inheritdoc/>
        protected override bool CheckInput(Transaction tx, int inputIndexCopy, TxOut txout, PrecomputedTransactionData txData, TxIn input, DeploymentFlags flags)
        {
            return this.logic.CheckInput(base.CheckInput, tx, inputIndexCopy, txout, txData, input, flags);
        }

        /// <summary>
        /// Executes contracts as necessary and updates the coinview / UTXOset after execution.
        /// </summary>
        /// <inheritdoc/>
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            this.logic.UpdateCoinView(base.UpdateUTXOSet, context, transaction);
        }
    }
}
