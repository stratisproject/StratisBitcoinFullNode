using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Xunit.Sdk;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    /// <summary>
    /// Concrete instance of the test chain.
    /// </summary>
    internal class TestRulesContext
    {
        public ConsensusRuleEngine ConsensusRuleEngine { get; set; }

        public IDateTimeProvider DateTimeProvider { get; set; }

        public ILoggerFactory LoggerFactory { get; set; }

        public NodeSettings NodeSettings { get; set; }

        public ChainIndexer ChainIndexer { get; set; }

        public Network Network { get; set; }

        public ICheckpoints Checkpoints { get; set; }

        public IChainState ChainState { get; set; }

        public ISignals Signals { get; set; }
        public IAsyncProvider AsyncProvider { get; internal set; }

        public T CreateRule<T>() where T : ConsensusRuleBase, new()
        {
            var rule = new T();
            rule.Parent = this.ConsensusRuleEngine;
            rule.Logger = this.LoggerFactory.CreateLogger(rule.GetType().FullName);
            rule.Initialize();
            return rule;
        }
    }

    public class RuleRegistrationHelper
    {
        private readonly ConsensusRuleEngine ruleEngine;
        private readonly ConsensusRulesContainer consensusRulesContainer;

        public RuleRegistrationHelper(ConsensusRuleEngine ruleEngine, ConsensusRulesContainer consensusRulesContainer)
        {
            this.ruleEngine = ruleEngine;
            this.consensusRulesContainer = consensusRulesContainer;
        }

        public T RegisterRule<T>() where T : ConsensusRuleBase, new()
        {
            var rule = new T();

            if (rule is IHeaderValidationConsensusRule validationConsensusRule)
                this.consensusRulesContainer.HeaderValidationRules = new List<HeaderValidationConsensusRule>() { validationConsensusRule as HeaderValidationConsensusRule };
            else if (rule is IIntegrityValidationConsensusRule consensusRule)
                this.consensusRulesContainer.IntegrityValidationRules = new List<IntegrityValidationConsensusRule>() { consensusRule as IntegrityValidationConsensusRule };
            else if (rule is IPartialValidationConsensusRule partialValidationConsensusRule)
                this.consensusRulesContainer.PartialValidationRules = new List<PartialValidationConsensusRule>() { partialValidationConsensusRule as PartialValidationConsensusRule };
            else if (rule is IFullValidationConsensusRule fullValidationConsensusRule)
                this.consensusRulesContainer.FullValidationRules = new List<FullValidationConsensusRule>() { fullValidationConsensusRule as FullValidationConsensusRule };
            else
                throw new Exception("Rule type wasn't recognized.");

            this.ruleEngine.SetupRulesEngineParent();
            return rule;
        }
    }

    /// <summary>
    /// Test consensus rules for unit tests.
    /// </summary>
    public class TestConsensusRules : ConsensusRuleEngine
    {
        public RuleContext RuleContext { get; set; }

        private RuleRegistrationHelper ruleRegistrationHelper;

        public TestConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ChainIndexer chainIndexer, NodeDeployments nodeDeployments,
            ConsensusSettings consensusSettings, ICheckpoints checkpoints, IChainState chainState, IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats, ConsensusRulesContainer consensusRulesContainer)
            : base(network, loggerFactory, dateTimeProvider, chainIndexer, nodeDeployments, consensusSettings, checkpoints, chainState, invalidBlockHashStore, nodeStats, consensusRulesContainer)
        {
            this.ruleRegistrationHelper = new RuleRegistrationHelper(this, consensusRulesContainer);
        }

        public T RegisterRule<T>() where T : ConsensusRuleBase, new()
        {
            return this.ruleRegistrationHelper.RegisterRule<T>();
        }

        public override RuleContext CreateRuleContext(ValidationContext validationContext)
        {
            return this.RuleContext ?? new PowRuleContext();
        }

        public override uint256 GetBlockHash()
        {
            throw new NotImplementedException();
        }

        public override Task<RewindState> RewindAsync()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Test PoS consensus rules for unit tests.
    /// </summary>
    public class TestPosConsensusRules : PosConsensusRuleEngine
    {
        private RuleRegistrationHelper ruleRegistrationHelper;

        public TestPosConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ChainIndexer chainIndexer,
            NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView uxtoSet, IStakeChain stakeChain,
            IStakeValidator stakeValidator, IChainState chainState, IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats, IRewindDataIndexCache rewindDataIndexCache, IAsyncProvider asyncProvider, ConsensusRulesContainer consensusRulesContainer)
            : base(network, loggerFactory, dateTimeProvider, chainIndexer, nodeDeployments, consensusSettings, checkpoints, uxtoSet, stakeChain, stakeValidator, chainState, invalidBlockHashStore, nodeStats, rewindDataIndexCache, asyncProvider, consensusRulesContainer)
        {
            this.ruleRegistrationHelper = new RuleRegistrationHelper(this, consensusRulesContainer);
        }

        public T RegisterRule<T>() where T : ConsensusRuleBase, new()
        {
            return this.ruleRegistrationHelper.RegisterRule<T>();
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

            testRulesContext.NodeSettings = new NodeSettings(network, args: new[] { $"-datadir={dataDir}" });
            testRulesContext.LoggerFactory = testRulesContext.NodeSettings.LoggerFactory;
            testRulesContext.LoggerFactory.AddConsoleWithFilters();
            testRulesContext.DateTimeProvider = DateTimeProvider.Default;
            network.Consensus.Options = new ConsensusOptions();

            var consensusSettings = new ConsensusSettings(testRulesContext.NodeSettings);
            testRulesContext.Checkpoints = new Checkpoints();
            testRulesContext.ChainIndexer = new ChainIndexer(network);
            testRulesContext.ChainState = new ChainState();
            testRulesContext.Signals = new Signals.Signals(testRulesContext.LoggerFactory, null);
            testRulesContext.AsyncProvider = new AsyncProvider(testRulesContext.LoggerFactory, testRulesContext.Signals, new NodeLifetime());

            var deployments = new NodeDeployments(testRulesContext.Network, testRulesContext.ChainIndexer);
            testRulesContext.ConsensusRuleEngine = new PowConsensusRuleEngine(testRulesContext.Network, testRulesContext.LoggerFactory, testRulesContext.DateTimeProvider,
                testRulesContext.ChainIndexer, deployments, consensusSettings, testRulesContext.Checkpoints, new InMemoryCoinView(new uint256()), testRulesContext.ChainState,
                new InvalidBlockHashStore(DateTimeProvider.Default), new NodeStats(DateTimeProvider.Default, testRulesContext.LoggerFactory), testRulesContext.AsyncProvider, new ConsensusRulesContainer()).SetupRulesEngineParent();

            return testRulesContext;
        }

        public static Block MineBlock(Network network, ChainIndexer chainIndexer)
        {
            var block = network.Consensus.ConsensusFactory.CreateBlock();
            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(chainIndexer.Height + 1));
            coinbase.AddOutput(new TxOut(Money.Zero, new Key()));
            block.AddTransaction(coinbase);

            block.Header.Version = (int)ThresholdConditionCache.VersionbitsTopBits;

            block.Header.HashPrevBlock = chainIndexer.Tip.HashBlock;
            block.Header.UpdateTime(DateTimeProvider.Default.GetTimeOffset(), network, chainIndexer.Tip);
            block.Header.Bits = block.Header.GetWorkRequired(network, chainIndexer.Tip);
            block.Header.Nonce = 0;

            int maxTries = int.MaxValue;

            while (maxTries > 0 && !block.CheckProofOfWork())
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