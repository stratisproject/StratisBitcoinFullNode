using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Xunit.Sdk;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    /// <summary>
    /// Concrete instance of the test chain.
    /// </summary>
    internal class TestRulesContext
    {
        public ConsensusRules Consensus { get; set; }

        public IDateTimeProvider DateTimeProvider { get; set; }

        public ILoggerFactory LoggerFactory { get; set; }

        public NodeSettings NodeSettings { get; set; }

        public ConcurrentChain Chain { get; set; }

        public Network Network { get; set; }

        public ICheckpoints Checkpoints { get; set; }

        public T CreateRule<T>() where T : ConsensusRule, new()
        {
            T rule = new T();
            rule.Parent = this.Consensus;
            rule.Logger = this.LoggerFactory.CreateLogger(rule.GetType().FullName);
            rule.Initialize();
            return rule;
        }
    }

    /// <summary>
    /// Test consensus rules for unit tests.
    /// </summary>
    public class TestConsensusRules : ConsensusRules
    {        
        private Mock<IRuleRegistration> ruleRegistration;

        public TestConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints)
        {
            this.ruleRegistration = new Mock<IRuleRegistration>();
        }

        public T RegisterRule<T>() where T : ConsensusRule, new()
        {
            T rule = new T();
            this.ruleRegistration.Setup(r => r.GetRules())
                .Returns(new List<ConsensusRule>() { rule });

            this.Register(this.ruleRegistration.Object);
            return rule;
        }       
    }

    /// <summary>
    /// Test PoS consensus rules for unit tests.
    /// </summary>
    public class TestPosConsensusRules : PosConsensusRules
    {
        private Mock<IRuleRegistration> ruleRegistration;

        public TestPosConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, CoinView uxtoSet, ILookaheadBlockPuller lookaheadBlockPuller, IStakeChain stakeChain, IStakeValidator stakeValidator)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, uxtoSet, lookaheadBlockPuller, stakeChain, stakeValidator)
        {
            this.ruleRegistration = new Mock<IRuleRegistration>();
        }

        public T RegisterRule<T>() where T : ConsensusRule, new()
        {
            T rule = new T();
            this.ruleRegistration.Setup(r => r.GetRules())
                .Returns(new List<ConsensusRule>() { rule });

            this.Register(this.ruleRegistration.Object);
            return rule;
        }
    }

    /// <summary>
    /// Factory for creating the test chain.
    /// Much of this logic was taken directly from the embedded TestContext class in MinerTest.cs in the integration tests.
    /// </summary>
    internal static class TestRulesContextFactory
    {
        /// <summary>
        /// Creates test chain with a consensus loop.
        /// </summary>
        public static TestRulesContext CreateAsync(Network network, [CallerMemberName]string pathName = null)
        {
            var testRulesContext = new TestRulesContext() { Network = network };

            string dataDir = Path.Combine("TestData", pathName);
            Directory.CreateDirectory(dataDir);

            testRulesContext.NodeSettings = new NodeSettings(network, args:new[] { $"-datadir={dataDir}" });
            testRulesContext.LoggerFactory = testRulesContext.NodeSettings.LoggerFactory;
            testRulesContext.LoggerFactory.AddConsoleWithFilters();
            testRulesContext.DateTimeProvider = DateTimeProvider.Default;
            network.Consensus.Options = new PowConsensusOptions();

            ConsensusSettings consensusSettings = new ConsensusSettings().Load(testRulesContext.NodeSettings);
            testRulesContext.Checkpoints = new Checkpoints();
            testRulesContext.Chain = new ConcurrentChain(network);

            NodeDeployments deployments = new NodeDeployments(testRulesContext.Network, testRulesContext.Chain);
            testRulesContext.Consensus = new PowConsensusRules(testRulesContext.Network, testRulesContext.LoggerFactory, testRulesContext.DateTimeProvider, testRulesContext.Chain, deployments, consensusSettings, testRulesContext.Checkpoints, new InMemoryCoinView(new uint256()), new Mock<ILookaheadBlockPuller>().Object).Register(new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration());

            return testRulesContext;
        }

        public static Block MineBlock(Network network, ConcurrentChain chain)
        {
            var block = new Block();
            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
            coinbase.AddOutput(new TxOut(Money.Zero, new Key()));
            block.AddTransaction(coinbase);

            block.Header.Version = (int)ThresholdConditionCache.VersionbitsTopBits;

            block.Header.HashPrevBlock = chain.Tip.HashBlock;
            block.Header.UpdateTime(DateTimeProvider.Default.GetTimeOffset(), network, chain.Tip);
            block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
            block.Header.Nonce = 0;

            var maxTries = int.MaxValue;

            while (maxTries > 0 && !block.CheckProofOfWork(network.Consensus))
            {
                ++block.Header.Nonce;
                --maxTries;
            }

            if (maxTries == 0)
                throw new XunitException("Test failed no blocks found");

            return block;
        }

    }
}
