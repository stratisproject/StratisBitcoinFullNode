﻿using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Xunit.Sdk;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    // Borrowed from Stratis.Bitcoin.Features.Consensus.Tests

    /// <summary>
    /// Concrete instance of the test chain.
    /// </summary>
    internal class TestRulesContext
    {
        public ConsensusRuleEngine Consensus { get; set; }

        public IDateTimeProvider DateTimeProvider { get; set; }

        public ILoggerFactory LoggerFactory { get; set; }

        public NodeSettings NodeSettings { get; set; }

        public ChainIndexer ChainIndexer { get; set; }

        public Network Network { get; set; }

        public ICheckpoints Checkpoints { get; set; }

        public IChainState ChainState { get; set; }

        public ISignals Signals { get; set; }

        public IAsyncProvider AsyncProvider { get; internal set; }

        public ICallDataSerializer CallDataSerializer { get; set; }

        public T CreateRule<T>() where T : ConsensusRuleBase, new()
        {
            T rule = new T();
            rule.Parent = this.Consensus;
            rule.Logger = this.LoggerFactory.CreateLogger(rule.GetType().FullName);
            rule.Initialize();
            return rule;
        }

        public ContractTransactionPartialValidationRule CreateContractValidationRule()
        {
            var rule = new ContractTransactionPartialValidationRule(this.CallDataSerializer, new List<IContractTransactionPartialValidationRule>
            {
                new SmartContractFormatLogic()
            });

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

            testRulesContext.NodeSettings = new NodeSettings(network, args: new[] { $"-datadir={dataDir}" });
            testRulesContext.LoggerFactory = testRulesContext.NodeSettings.LoggerFactory;
            testRulesContext.LoggerFactory.AddConsoleWithFilters();
            testRulesContext.DateTimeProvider = DateTimeProvider.Default;

            new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(network.Consensus);

            ConsensusSettings consensusSettings = new ConsensusSettings(testRulesContext.NodeSettings);
            testRulesContext.Checkpoints = new Checkpoints();
            testRulesContext.ChainIndexer = new ChainIndexer(network);
            testRulesContext.ChainState = new ChainState();

            testRulesContext.Signals = new Signals.Signals(testRulesContext.LoggerFactory, null);
            testRulesContext.AsyncProvider = new AsyncProvider(testRulesContext.LoggerFactory, testRulesContext.Signals, new NodeLifetime());

            NodeDeployments deployments = new NodeDeployments(testRulesContext.Network, testRulesContext.ChainIndexer);

            testRulesContext.Consensus = new PowConsensusRuleEngine(testRulesContext.Network, testRulesContext.LoggerFactory, testRulesContext.DateTimeProvider,
                testRulesContext.ChainIndexer, deployments, consensusSettings, testRulesContext.Checkpoints, null, testRulesContext.ChainState,
                new InvalidBlockHashStore(new DateTimeProvider()), new NodeStats(new DateTimeProvider()), testRulesContext.AsyncProvider).Register();

            testRulesContext.CallDataSerializer = new CallDataSerializer(new ContractPrimitiveSerializer(network));
            return testRulesContext;
        }

        public static Block MineBlock(Network network, ChainIndexer chainIndexer)
        {
            Block block = network.Consensus.ConsensusFactory.CreateBlock();

            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(chainIndexer.Height + 1));
            coinbase.AddOutput(new TxOut(Money.Zero, new Key()));
            block.AddTransaction(coinbase);

            block.Header.Version = (int)ThresholdConditionCache.VersionbitsTopBits;

            block.Header.HashPrevBlock = chainIndexer.Tip.HashBlock;
            block.Header.UpdateTime(DateTimeProvider.Default.GetTimeOffset(), network, chainIndexer.Tip);
            block.Header.Bits = block.Header.GetWorkRequired(network, chainIndexer.Tip);
            block.Header.Nonce = 0;

            var maxTries = int.MaxValue;

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