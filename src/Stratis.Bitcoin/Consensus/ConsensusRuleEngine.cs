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
using Stratis.Bitcoin.Primitives;
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
        public IConsensus ConsensusParams { get; }

        /// <summary>Consensus settings for the full node.</summary>
        public ConsensusSettings ConsensusSettings { get; }

        /// <summary>Provider of block header hash checkpoints.</summary>
        public ICheckpoints Checkpoints { get; }

        /// <summary>State of the current chain that hold consensus tip.</summary>
        public IChainState ChainState { get; }

        /// <inheritdoc cref="IInvalidBlockHashStore"/>
        private readonly IInvalidBlockHashStore invalidBlockHashStore;

        /// <inheritdoc />
        public ConsensusPerformanceCounter PerformanceCounter { get; }

        /// <summary>Group of rules that are used during block header validation.</summary>
        private List<HeaderValidationConsensusRule> headerValidationRules;

        /// <summary>Group of rules that are used during block integrity validation.</summary>
        private List<IntegrityValidationConsensusRule> integrityValidationRules;

        /// <summary>Group of rules that are used during partial block validation.</summary>
        private List<PartialValidationConsensusRule> partialValidationRules;

        /// <summary>Group of rules that are used during full validation (connection of a new block).</summary>
        private List<FullValidationConsensusRule> fullValidationRules;

        protected ConsensusRuleEngine(
            Network network,
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider,
            ConcurrentChain chain,
            NodeDeployments nodeDeployments,
            ConsensusSettings consensusSettings,
            ICheckpoints checkpoints,
            IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(nodeDeployments, nameof(nodeDeployments));
            Guard.NotNull(consensusSettings, nameof(consensusSettings));
            Guard.NotNull(checkpoints, nameof(checkpoints));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(invalidBlockHashStore, nameof(invalidBlockHashStore));

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
            this.invalidBlockHashStore = invalidBlockHashStore;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.NodeDeployments = nodeDeployments;
            this.PerformanceCounter = new ConsensusPerformanceCounter(this.DateTimeProvider);

            this.headerValidationRules = new List<HeaderValidationConsensusRule>();
            this.integrityValidationRules = new List<IntegrityValidationConsensusRule>();
            this.partialValidationRules = new List<PartialValidationConsensusRule>();
            this.fullValidationRules = new List<FullValidationConsensusRule>();
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
            this.headerValidationRules = this.Network.Consensus.HeaderValidationRules.Select(x => x as HeaderValidationConsensusRule).ToList();
            this.SetupConsensusRules(this.headerValidationRules.Select(x => x as ConsensusRuleBase));

            this.integrityValidationRules = this.Network.Consensus.IntegrityValidationRules.Select(x => x as IntegrityValidationConsensusRule).ToList();
            this.SetupConsensusRules(this.integrityValidationRules.Select(x => x as ConsensusRuleBase));

            this.partialValidationRules = this.Network.Consensus.PartialValidationRules.Select(x => x as PartialValidationConsensusRule).ToList();
            this.SetupConsensusRules(this.partialValidationRules.Select(x => x as ConsensusRuleBase));

            this.fullValidationRules = this.Network.Consensus.FullValidationRules.Select(x => x as FullValidationConsensusRule).ToList();
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
        public ValidationContext HeaderValidation(ChainedHeader header, bool blockMined)
        {
            Guard.NotNull(header, nameof(header));

            var validationContext = new ValidationContext { ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext, blockMined);

            this.ExecuteRules(this.headerValidationRules, ruleContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public ValidationContext IntegrityValidation(ChainedHeader header, Block block, bool blockMined)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext, blockMined);

            this.ExecuteRules(this.integrityValidationRules, ruleContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public async Task<ValidationContext> FullValidationAsync(ChainedHeaderBlock chainedHeaderBlock, bool blockMined)
        {
            Guard.NotNull(chainedHeaderBlock, nameof(chainedHeaderBlock));

            var validationContext = new ValidationContext { BlockToValidate = chainedHeaderBlock.Block, ChainedHeaderToValidate = chainedHeaderBlock.ChainedHeader };
            RuleContext ruleContext = this.CreateRuleContext(validationContext, blockMined);

            await this.ExecuteRulesAsync(this.fullValidationRules, ruleContext).ConfigureAwait(false);

            if (validationContext.Error != null)
                this.HandleConsensusError(validationContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public async Task<ValidationContext> PartialValidationAsync(ChainedHeader header, Block block, bool blockMined)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext, blockMined);

            await this.ExecuteRulesAsync(this.partialValidationRules, ruleContext).ConfigureAwait(false);

            if (validationContext.Error != null)
                this.HandleConsensusError(validationContext);

            return validationContext;
        }

        private async Task ExecuteRulesAsync(IEnumerable<AsyncConsensusRule> asyncRules, RuleContext ruleContext)
        {
            try
            {
                using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
                {
                    ruleContext.SkipValidation = ruleContext.ValidationContext.ChainedHeaderToValidate.IsAssumedValid;

                    foreach (AsyncConsensusRule rule in asyncRules)
                        await rule.RunAsync(ruleContext).ConfigureAwait(false);
                }
            }
            catch (ConsensusErrorException ex)
            {
                ruleContext.ValidationContext.Error = ex.ConsensusError;
            }
        }

        /// <summary>Adds block hash to a list of failed header unless specific consensus error was used that doesn't require block banning.</summary>
        private void HandleConsensusError(ValidationContext validationContext)
        {
            this.logger.LogTrace("()");

            // This error will be handled in ConsensusManager.
            // The block shouldn't be banned because it might be valid, it's just we need to redownload it including witness data.
            if (validationContext.Error == ConsensusErrors.BadWitnessNonceSize)
            {
                this.logger.LogTrace("(-)[BAD_WITNESS_NONCE]");
                return;
            }

            uint256 hashToBan = validationContext.ChainedHeaderToValidate.HashBlock;

            this.logger.LogTrace("Marking '{0}' invalid.", hashToBan);
            this.invalidBlockHashStore.MarkInvalid(hashToBan, validationContext.RejectUntil);

            this.logger.LogTrace("(-)");
        }

        private void ExecuteRules(IEnumerable<SyncConsensusRule> rules, RuleContext ruleContext)
        {
            try
            {
                using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
                {
                    ruleContext.SkipValidation = ruleContext.ValidationContext.ChainedHeaderToValidate.IsAssumedValid;

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
        public abstract RuleContext CreateRuleContext(ValidationContext validationContext, bool blockMined);

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
