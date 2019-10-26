using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus.PerformanceCounters.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Consensus
{
    /// <inheritdoc />
    public abstract class ConsensusRuleEngine : IConsensusRuleEngine
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>A factory to creates logger instances for each rule.</summary>
        public ILoggerFactory LoggerFactory { get; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; }

        /// <summary>A provider of date and time.</summary>
        public IDateTimeProvider DateTimeProvider { get; }

        /// <summary>A chain of the longest block headers all the way to genesis.</summary>
        public ChainIndexer ChainIndexer { get; }

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

        private readonly ConsensusRulesContainer consensusRules;

        /// <inheritdoc cref="ConsensusRulesPerformanceCounter"/>
        private ConsensusRulesPerformanceCounter performanceCounter;

        protected ConsensusRuleEngine(
            Network network,
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider,
            ChainIndexer chainIndexer,
            NodeDeployments nodeDeployments,
            ConsensusSettings consensusSettings,
            ICheckpoints checkpoints,
            IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore,
            INodeStats nodeStats,
            ConsensusRulesContainer consensusRules)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(chainIndexer, nameof(chainIndexer));
            Guard.NotNull(nodeDeployments, nameof(nodeDeployments));
            Guard.NotNull(consensusSettings, nameof(consensusSettings));
            Guard.NotNull(checkpoints, nameof(checkpoints));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(invalidBlockHashStore, nameof(invalidBlockHashStore));
            Guard.NotNull(nodeStats, nameof(nodeStats));

            this.Network = network;
            this.ChainIndexer = chainIndexer;
            this.ChainState = chainState;
            this.NodeDeployments = nodeDeployments;
            this.LoggerFactory = loggerFactory;
            this.ConsensusSettings = consensusSettings;
            this.Checkpoints = checkpoints;
            this.ConsensusParams = this.Network.Consensus;
            this.ConsensusSettings = consensusSettings;
            this.DateTimeProvider = dateTimeProvider;
            this.invalidBlockHashStore = invalidBlockHashStore;
            this.consensusRules = consensusRules;
            this.LoggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.NodeDeployments = nodeDeployments;

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, this.GetType().Name, 500);
        }

        /// <inheritdoc />
        public virtual void Initialize(ChainedHeader chainTip)
        {
            this.SetupRulesEngineParent();
        }

        /// <inheritdoc />
        public virtual void Dispose()
        {
        }

        /// TODO: this method needs to be deleted once all rules use dependency injection
        /// <inheritdoc />
        [NoTrace]
        public ConsensusRuleEngine SetupRulesEngineParent()
        {
            this.SetupConsensusRules(this.consensusRules.HeaderValidationRules.Select(x => x as ConsensusRuleBase));
            this.SetupConsensusRules(this.consensusRules.IntegrityValidationRules.Select(x => x as ConsensusRuleBase));
            this.SetupConsensusRules(this.consensusRules.PartialValidationRules.Select(x => x as ConsensusRuleBase));
            this.SetupConsensusRules(this.consensusRules.FullValidationRules.Select(x => x as ConsensusRuleBase));

            // Registers all rules that might be executed to the performance counter.
            this.performanceCounter = new ConsensusRulesPerformanceCounter(this.consensusRules);

            return this;
        }

        [NoTrace]
        private void SetupConsensusRules(IEnumerable<ConsensusRuleBase> rules)
        {
            foreach (ConsensusRuleBase rule in rules)
            {
                rule.Parent = this;
                rule.Logger = this.LoggerFactory.CreateLogger(rule.GetType().FullName);
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
        public virtual ValidationContext HeaderValidation(ChainedHeader header)
        {
            Guard.NotNull(header, nameof(header));

            var validationContext = new ValidationContext { ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            this.ExecuteRules(this.consensusRules.HeaderValidationRules, ruleContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public virtual ValidationContext IntegrityValidation(ChainedHeader header, Block block)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            this.ExecuteRules(this.consensusRules.IntegrityValidationRules, ruleContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public virtual async Task<ValidationContext> FullValidationAsync(ChainedHeader header, Block block)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            await this.ExecuteRulesAsync(this.consensusRules.FullValidationRules, ruleContext).ConfigureAwait(false);

            if (validationContext.Error != null)
            {
                this.logger.LogWarning("Block '{0}' failed full validation with error: '{1}'.", header, validationContext.Error);
                this.HandleConsensusError(validationContext);
            }

            return validationContext;
        }

        /// <inheritdoc/>
        public virtual async Task<ValidationContext> PartialValidationAsync(ChainedHeader header, Block block)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            await this.ExecuteRulesAsync(this.consensusRules.PartialValidationRules, ruleContext).ConfigureAwait(false);

            if (validationContext.Error != null)
            {
                this.logger.LogWarning("Block '{0}' failed partial validation with error: '{1}'.", header, validationContext.Error);
                this.HandleConsensusError(validationContext);
            }

            return validationContext;
        }

        private async Task ExecuteRulesAsync(IEnumerable<AsyncConsensusRule> asyncRules, RuleContext ruleContext)
        {
            try
            {
                ruleContext.SkipValidation = ruleContext.ValidationContext.ChainedHeaderToValidate.IsAssumedValid;

                foreach (AsyncConsensusRule rule in asyncRules)
                {
                    using (this.performanceCounter.MeasureRuleExecutionTime(rule))
                    {
                        await rule.RunAsync(ruleContext).ConfigureAwait(false);
                    }
                }
            }
            catch (ConsensusErrorException ex)
            {
                ruleContext.ValidationContext.Error = ex.ConsensusError;
            }
            catch (Exception exception)
            {
                this.logger.LogCritical("Unhandled exception in consensus rules engine: {0}.", exception.ToString());
                throw;
            }
        }

        /// <summary>Adds block hash to a list of failed header unless specific consensus error was used that doesn't require block banning.</summary>
        private void HandleConsensusError(ValidationContext validationContext)
        {
            // This error will be handled in ConsensusManager.
            // The block shouldn't be banned because it might be valid, it's just we need to redownload it including witness data.
            if (validationContext.Error == ConsensusErrors.BadWitnessNonceSize)
            {
                this.logger.LogTrace("(-)[BAD_WITNESS_NONCE]");
                return;
            }

            uint256 hashToBan = validationContext.ChainedHeaderToValidate.HashBlock;

            this.logger.LogWarning("Marking '{0}' invalid.", hashToBan);
            this.invalidBlockHashStore.MarkInvalid(hashToBan, validationContext.RejectUntil);
        }

        private void ExecuteRules(IEnumerable<SyncConsensusRule> rules, RuleContext ruleContext)
        {
            try
            {
                ruleContext.SkipValidation = ruleContext.ValidationContext.ChainedHeaderToValidate.IsAssumedValid;

                foreach (SyncConsensusRule rule in rules)
                {
                    using (this.performanceCounter.MeasureRuleExecutionTime(rule))
                    {
                        rule.Run(ruleContext);
                    }
                }
            }
            catch (ConsensusErrorException ex)
            {
                ruleContext.ValidationContext.Error = ex.ConsensusError;
            }
            catch (Exception exception)
            {
                this.logger.LogCritical("Unhandled exception in consensus rules engine: {0}.", exception.ToString());
                throw;
            }
        }

        /// <inheritdoc />
        public abstract RuleContext CreateRuleContext(ValidationContext validationContext);

        /// <inheritdoc />
        public abstract uint256 GetBlockHash();

        /// <inheritdoc />
        public abstract Task<RewindState> RewindAsync();

        [NoTrace]
        public T GetRule<T>() where T : ConsensusRuleBase
        {
            object rule = this.consensusRules.HeaderValidationRules.OfType<T>().SingleOrDefault();

            if (rule == null)
                rule = this.consensusRules.IntegrityValidationRules.OfType<T>().SingleOrDefault();

            if (rule == null)
                rule = this.consensusRules.PartialValidationRules.OfType<T>().SingleOrDefault();

            if (rule == null)
                rule = this.consensusRules.FullValidationRules.OfType<T>().SingleOrDefault();

            return rule as T;
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            benchLog.AppendLine(this.performanceCounter.TakeSnapshot().ToString());
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
