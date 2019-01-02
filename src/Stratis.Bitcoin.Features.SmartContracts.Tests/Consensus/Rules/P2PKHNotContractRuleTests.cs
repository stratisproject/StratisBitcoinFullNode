using System;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class P2PKHNotContractRuleTests
    {
        private readonly Network network;

        private readonly TestContractRulesEngine rulesEngine;

        public P2PKHNotContractRuleTests()
        {
            this.network = new SmartContractsRegTest();

            var loggerFactory = new Mock<ILoggerFactory>();
            var dateTimeProvider = new Mock<IDateTimeProvider>();
            var chain = new Mock<ConcurrentChain>();
            var nodeDeployments = new Mock<NodeDeployments>();
            var consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network));
            var checkpoints = new Mock<ICheckpoints>();
            var coinView = new Mock<ICoinView>();
            var chainState = new Mock<ChainState>();
            var invalidBlockHashStore = new Mock<IInvalidBlockHashStore>();

            this.rulesEngine = new TestContractRulesEngine(this.network,
                loggerFactory.Object,
                dateTimeProvider.Object,
                chain.Object,
                new NodeDeployments(this.network, chain.Object),
                consensusSettings,
                checkpoints.Object,
                coinView.Object,
                chainState.Object,
                invalidBlockHashStore.Object,
                new NodeStats(new DateTimeProvider())
            );
        }

        [Fact]
        public void SendTo_NotAContract_Success()
        {
            uint160 walletAddress = new uint160(321);

            var state = new Mock<IStateRepositoryRoot>();
            state.Setup(x => x.GetAccountState(walletAddress)).Returns<AccountState>(null);
            this.rulesEngine.OriginalStateRoot = state.Object;

            var rule = new P2PKHNotContractRule();
            rule.Parent = this.rulesEngine;
            rule.Initialize();

            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(100, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(walletAddress))));
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        [Fact]
        public void SendTo_Contract_Fails()
        {
            uint160 contractAddress = new uint160(123);

            var state = new Mock<IStateRepositoryRoot>();
            state.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState()); // not null
            this.rulesEngine.OriginalStateRoot = state.Object;

            var rule = new P2PKHNotContractRule();
            rule.Parent = this.rulesEngine;
            rule.Initialize();

            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(100, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(contractAddress))));
            Assert.Throws<ConsensusErrorException>(() => rule.CheckTransaction(new MempoolValidationContext(transaction, null)));
        }
    }

    public class TestContractRulesEngine : PowConsensusRuleEngine, ISmartContractCoinviewRule
    {
        public TestContractRulesEngine(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView utxoSet, IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState, invalidBlockHashStore, nodeStats)
        {
        }

        public ICallDataSerializer CallDataSerializer => throw new NotImplementedException();

        public IContractExecutorFactory ExecutorFactory => throw new NotImplementedException();

        public IStateRepositoryRoot OriginalStateRoot { get; set; }

        public IReceiptRepository ReceiptRepository => throw new NotImplementedException();

        public ISenderRetriever SenderRetriever => throw new NotImplementedException();
    }
}
