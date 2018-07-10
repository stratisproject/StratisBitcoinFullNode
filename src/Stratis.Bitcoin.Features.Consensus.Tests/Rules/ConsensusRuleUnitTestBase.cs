using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    public class ConsensusRuleUnitTestBase
    {
        protected Network network;
        protected Mock<ILogger> logger;
        protected Mock<ILoggerFactory> loggerFactory;
        protected Mock<IDateTimeProvider> dateTimeProvider;
        protected ConcurrentChain concurrentChain;
        protected NodeDeployments nodeDeployments;
        protected ConsensusSettings consensusSettings;
        protected Mock<ICheckpoints> checkpoints;
        protected List<ConsensusRule> ruleRegistrations;
        protected Mock<IRuleRegistration> ruleRegistration;
        protected RuleContext ruleContext;
        protected Transaction lastAddedTransaction;

        protected ConsensusRuleUnitTestBase(Network network)
        {
            this.network = network;
            this.logger = new Mock<ILogger>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            this.dateTimeProvider = new Mock<IDateTimeProvider>();

            this.concurrentChain = new ConcurrentChain(this.network);
            this.nodeDeployments = new NodeDeployments(this.network, this.concurrentChain);
            this.consensusSettings = new ConsensusSettings();
            this.checkpoints = new Mock<ICheckpoints>();

            this.ruleRegistrations = new List<ConsensusRule>();
            this.ruleRegistration = new Mock<IRuleRegistration>();
            this.ruleRegistration.Setup(r => r.GetRules())
                .Returns(() => { return this.ruleRegistrations; });

            if (network.Consensus.IsProofOfStake)
            {
                this.ruleContext = new PosRuleContext(new ValidationContext(), this.network.Consensus, this.concurrentChain.Tip);
            }
            else
            {
                this.ruleContext = new PowRuleContext(new ValidationContext(), this.network.Consensus, this.concurrentChain.Tip);
            }
        }

        protected void AddBlocksToChain(ConcurrentChain chain, int blockAmount)
        {
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Tip.HashBlock;

            (this.ruleContext as UtxoRuleContext).UnspentOutputSet = new UnspentOutputSet();
            (this.ruleContext as UtxoRuleContext).UnspentOutputSet.SetCoins(new UnspentOutputs[0]);

            for (int i = 0; i < blockAmount; i++)
            {
                Block block = chain.Network.Consensus.ConsensusFactory.CreateBlock();
                Transaction transaction = chain.Network.CreateTransaction();
                block.AddTransaction(transaction);
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
                (this.ruleContext as UtxoRuleContext).UnspentOutputSet.Update(transaction, i);
                this.lastAddedTransaction = transaction;
            }
        }
    }

    public class PosConsensusRuleUnitTestBase : ConsensusRuleUnitTestBase
    {
        protected Mock<IStakeChain> stakeChain;
        protected Mock<IStakeValidator> stakeValidator;
        protected Mock<ILookaheadBlockPuller> lookaheadBlockPuller;
        protected Mock<CoinView> coinView;

        public PosConsensusRuleUnitTestBase() : base(Network.StratisTest)
        {
            this.stakeChain = new Mock<IStakeChain>();
            this.stakeValidator = new Mock<IStakeValidator>();
            this.lookaheadBlockPuller = new Mock<ILookaheadBlockPuller>();
            this.coinView = new Mock<CoinView>();
        }
    }

    public class ConsensusRuleUnitTestBase<T> where T : ConsensusRules
    {
        protected Network network;
        protected Mock<ILoggerFactory> loggerFactory;
        protected Mock<IDateTimeProvider> dateTimeProvider;
        protected ConcurrentChain concurrentChain;
        protected NodeDeployments nodeDeployments;
        protected ConsensusSettings consensusSettings;
        protected Mock<ICheckpoints> checkpoints;
        protected List<ConsensusRule> ruleRegistrations;
        protected Mock<IRuleRegistration> ruleRegistration;
        protected T consensusRules;
        protected RuleContext ruleContext;

        protected ConsensusRuleUnitTestBase(Network network)
        {
            this.network = network;
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            this.dateTimeProvider = new Mock<IDateTimeProvider>();

            this.concurrentChain = new ConcurrentChain(this.network);
            this.nodeDeployments = new NodeDeployments(this.network, this.concurrentChain);
            this.consensusSettings = new ConsensusSettings();
            this.checkpoints = new Mock<ICheckpoints>();

            this.ruleRegistrations = new List<ConsensusRule>();
            this.ruleRegistration = new Mock<IRuleRegistration>();
            this.ruleRegistration.Setup(r => r.GetRules())
                .Returns(() => { return this.ruleRegistrations; });

            if (network.Consensus.IsProofOfStake)
            {
                this.ruleContext = new PosRuleContext(new ValidationContext(), this.network.Consensus, this.concurrentChain.Tip);
            }
            else
            {
                this.ruleContext = new PowRuleContext(new ValidationContext(), this.network.Consensus, this.concurrentChain.Tip);
            }
        }

        public virtual T InitializeConsensusRules()
        {
            throw new NotImplementedException("override and initialize the consensusrules!");
        }

        protected static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        protected static ConcurrentChain MineChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = TestRulesContextFactory.MineBlock(network, chain);
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }
    }


    public class TestConsensusRulesUnitTestBase : ConsensusRuleUnitTestBase<TestConsensusRules>
    {
        public TestConsensusRulesUnitTestBase() : base(Network.TestNet)
        {
            this.network.Consensus.Options = new PowConsensusOptions();
            this.consensusRules = InitializeConsensusRules();
        }

        public override TestConsensusRules InitializeConsensusRules()
        {
            return new TestConsensusRules(this.network, this.loggerFactory.Object, this.dateTimeProvider.Object, this.concurrentChain, this.nodeDeployments, this.consensusSettings, this.checkpoints.Object);
        }
    }

    public class TestPosConsensusRulesUnitTestBase : ConsensusRuleUnitTestBase<TestPosConsensusRules>
    {
        protected Mock<IStakeChain> stakeChain;
        protected Mock<IStakeValidator> stakeValidator;
        protected Mock<ILookaheadBlockPuller> lookaheadBlockPuller;
        protected Mock<CoinView> coinView;

        public TestPosConsensusRulesUnitTestBase() : base(Network.StratisTest)
        {
            this.stakeChain = new Mock<IStakeChain>();
            this.stakeValidator = new Mock<IStakeValidator>();
            this.lookaheadBlockPuller = new Mock<ILookaheadBlockPuller>();
            this.coinView = new Mock<CoinView>();
            this.consensusRules = InitializeConsensusRules();
        }

        public override TestPosConsensusRules InitializeConsensusRules()
        {
            return new TestPosConsensusRules(this.network, this.loggerFactory.Object, this.dateTimeProvider.Object, this.concurrentChain, this.nodeDeployments, this.consensusSettings, this.checkpoints.Object, this.coinView.Object, this.lookaheadBlockPuller.Object, this.stakeChain.Object, this.stakeValidator.Object);
        }
    }
}
