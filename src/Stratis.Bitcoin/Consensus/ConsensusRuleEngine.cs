﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <inheritdoc />
    public abstract class ConsensusRuleEngine : IConsensusRuleEngine
    {
        /// <summary>A factory to creates logger instances for each rule.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        protected readonly ILogger logger;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; }

        /// <summary>A provider of date and time.</summary>
        public IDateTimeProvider DateTimeProvider { get; }

        /// <summary>A chain of the longest block headers all the way to genesis.</summary>
        public ConcurrentChain Chain { get; }

        /// <summary>A deployment construction that tracks activation of features on the chain.</summary>
        public NodeDeployments NodeDeployments { get; }

        /// <summary>A collection of consensus constants.</summary>
        public NBitcoin.Consensus ConsensusParams { get; }

        /// <summary>Consensus settings for the full node.</summary>
        public ConsensusSettings ConsensusSettings { get; }

        /// <summary>Provider of block header hash checkpoints.</summary>
        public ICheckpoints Checkpoints { get; }

        /// <summary>State of the current chain that hold consensus tip.</summary>
        public IChainState ChainState { get; }

        /// <inheritdoc />
        public ConsensusPerformanceCounter PerformanceCounter { get; }

        /// <summary>Group of rules that are used during block's header validation.</summary>
        private List<SyncConsensusRule> headerValidationRules;

        /// <summary>Group of rules that are used during block integrity validation.</summary>
        private List<SyncConsensusRule> integrityValidationRules;

        /// <summary>Group of rules that are used during partial block validation.</summary>
        private List<AsyncConsensusRule> partialValidationRules;

        /// <summary>Group of rules that are used during full validation (connection of a new block).</summary>
        private List<AsyncConsensusRule> fullValidationRules;

        protected ConsensusRuleEngine(
            Network network,
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider,
            ConcurrentChain chain,
            NodeDeployments nodeDeployments,
            ConsensusSettings consensusSettings,
            ICheckpoints checkpoints,
            IChainState chainState)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(nodeDeployments, nameof(nodeDeployments));
            Guard.NotNull(consensusSettings, nameof(consensusSettings));
            Guard.NotNull(checkpoints, nameof(checkpoints));
            Guard.NotNull(chainState, nameof(chainState));

            this.Network = network;

            this.Chain = chain;
            this.ChainState = chainState;
            this.NodeDeployments = nodeDeployments;
            this.loggerFactory = loggerFactory;
            this.ConsensusSettings = consensusSettings;
            this.Checkpoints = checkpoints;
            this.ConsensusParams = this.Network.Consensus;
            this.ConsensusSettings = consensusSettings;
            this.DateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.NodeDeployments = nodeDeployments;
            this.PerformanceCounter = new ConsensusPerformanceCounter(this.DateTimeProvider);

            this.headerValidationRules = new List<SyncConsensusRule>();
            this.integrityValidationRules = new List<SyncConsensusRule>();
            this.partialValidationRules = new List<AsyncConsensusRule>();
            this.fullValidationRules = new List<AsyncConsensusRule>();
        }

        /// <inheritdoc />
        public virtual Task Initialize()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual void Dispose()
        {
        }

        /// <inheritdoc />
        public ConsensusRuleEngine Register()
        {
            this.headerValidationRules = this.Network.Consensus.HeaderValidationRules.Select(x => x as SyncConsensusRule).ToList();
            this.SetupConsensusRules(this.headerValidationRules.Select(x => x as ConsensusRuleBase));

            this.integrityValidationRules = this.Network.Consensus.IntegrityValidationRules.Select(x => x as SyncConsensusRule).ToList();
            this.SetupConsensusRules(this.integrityValidationRules.Select(x => x as ConsensusRuleBase));

            this.partialValidationRules = this.Network.Consensus.PartialValidationRules.Select(x => x as AsyncConsensusRule).ToList();
            this.SetupConsensusRules(this.partialValidationRules.Select(x => x as ConsensusRuleBase));

            this.fullValidationRules = this.Network.Consensus.FullValidationRules.Select(x => x as AsyncConsensusRule).ToList();
            this.SetupConsensusRules(this.fullValidationRules.Select(x => x as ConsensusRuleBase));

            return this;
        }

        private void SetupConsensusRules(IEnumerable<ConsensusRuleBase> rules)
        {
            foreach (ConsensusRuleBase rule in rules)
            {
                rule.Parent = this;
                rule.Logger = this.loggerFactory.CreateLogger(rule.GetType().FullName);
                rule.Initialize();
            }
        }

        public void SetErrorHandler(ConsensusRuleBase errorHandler)
        {
            // TODO: set a rule that will be invoked when a validation of a block failed.

            // This will allow the creator of the blockchain to provide
            // an error handler rule that is unique to the current blockchain.
            // future sidechains may have additional handling of errors that
            // extend or replace the default current error handling code.
        }

        /// <inheritdoc/>
        public void HeaderValidation(ValidationContext validationContext)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            this.ExecuteRules(this.headerValidationRules, ruleContext);
        }

        /// <inheritdoc/>
        public void IntegrityValidation(ValidationContext validationContext)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            this.ExecuteRules(this.integrityValidationRules, ruleContext);
        }

        /// <inheritdoc/>
        public async Task FullValidationAsync(ValidationContext validationContext)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            try
            {
                await this.ExecuteRulesAsync(this.fullValidationRules, ruleContext).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                this.logger.LogCritical("Full Validation error: {0}", exception.ToString());
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task PartialValidationAsync(ValidationContext validationContext)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            try
            {
                await this.ExecuteRulesAsync(this.partialValidationRules, ruleContext).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                this.logger.LogCritical("Partial Validation error: {0}", exception.ToString());
                throw;
            }
        }

        private async Task ExecuteRulesAsync(List<AsyncConsensusRule> asyncRules, RuleContext ruleContext)
        {
            try
            {
                using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
                {
                    ruleContext.SkipValidation = ruleContext.ValidationContext.ChainTipToExtend.IsAssumedValid;

                    foreach (AsyncConsensusRule rule in asyncRules)
                        await rule.RunAsync(ruleContext).ConfigureAwait(false);
                }
            }
            catch (ConsensusErrorException ex)
            {
                ruleContext.ValidationContext.Error = ex.ConsensusError;
            }
        }

        private void ExecuteRules(List<SyncConsensusRule> rules, RuleContext ruleContext)
        {
            try
            {
                using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
                {
                    ruleContext.SkipValidation = ruleContext.ValidationContext.ChainTipToExtend.IsAssumedValid;

                    foreach (SyncConsensusRule rule in rules)
                        rule.Run(ruleContext);
                }
            }
            catch (ConsensusErrorException ex)
            {
                ruleContext.ValidationContext.Error = ex.ConsensusError;
            }
        }

        /// <inheritdoc />
        public abstract RuleContext CreateRuleContext(ValidationContext validationContext);

        /// <inheritdoc />
        public abstract Task<uint256> GetBlockHashAsync();

        /// <inheritdoc />
        public abstract Task<RewindState> RewindAsync();

        public T GetRule<T>() where T : ConsensusRuleBase
        {
            object rule = this.headerValidationRules.SingleOrDefault(r => r is T);

            if (rule == null)
                rule = this.integrityValidationRules.SingleOrDefault(r => r is T);

            if (rule == null)
                rule = this.partialValidationRules.SingleOrDefault(r => r is T);

            if (rule == null)
                rule = this.fullValidationRules.SingleOrDefault(r => r is T);

            return rule as T;
        }
    }

    /// <summary>
    /// A class that is used to store transitions of state of consensus underline storage.
    /// </summary>
    /// <remarks>
    /// A transition state can have transition information of several consecutive block,
    /// The <see cref="BlockHash"/> parameter represents the tip of the consecutive list of blocks.
    /// </remarks>
    public class RewindState
    {
        /// <summary>
        /// The block hash that represents the tip of the transition.
        /// </summary>
        public uint256 BlockHash { get; set; }
    }
}
