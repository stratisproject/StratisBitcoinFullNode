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
        protected readonly ILogger logger;

        /// <summary>A factory to creates logger instances for each rule.</summary>
        public ILoggerFactory LoggerFactory { get; }

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

        /// <inheritdoc cref="ConsensusRulesPerformanceCounter"/>
        private ConsensusRulesPerformanceCounter performanceCounter;

        /// <summary>
        /// Backing field for the <see cref="RuleContainer"/> property.
        /// </summary>
        private RuleContainer ruleContainer;

        /// <summary>
        /// Backing field for the <see cref="RuleRegistration"/> property.
        /// </summary>
        private IRuleRegistration ruleRegistration;

        protected ConsensusRuleEngine(
            Network network,
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider,
            ConcurrentChain chain,
            NodeDeployments nodeDeployments,
            ConsensusSettings consensusSettings,
            ICheckpoints checkpoints,
            IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore,
            INodeStats nodeStats,
            IRuleRegistration ruleRegistration)
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
            Guard.NotNull(nodeStats, nameof(nodeStats));
            Guard.NotNull(ruleRegistration, nameof(ruleRegistration));

            this.Network = network;
            this.Chain = chain;
            this.ChainState = chainState;
            this.NodeDeployments = nodeDeployments;
            this.LoggerFactory = loggerFactory;
            this.ConsensusSettings = consensusSettings;
            this.Checkpoints = checkpoints;
            this.ConsensusParams = this.Network.Consensus;
            this.ConsensusSettings = consensusSettings;
            this.DateTimeProvider = dateTimeProvider;
            this.invalidBlockHashStore = invalidBlockHashStore;
            this.LoggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.NodeDeployments = nodeDeployments;

            // Set the private field and not the property.
            // Setting the property will cause rules to be registered here, which we do not want.
            this.ruleRegistration = ruleRegistration;

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 500);
        }

        public RuleContainer RuleContainer
        {
            get
            {
                // Workaround for removing the ability to call Register externally.
                // We do not want to call Register in the constructor due to overrides
                // setting additional dependencies *after* Register has been called.
                // This can be removed once full rule DI is in place.
                if (this.ruleContainer == null)
                {
                    this.ruleContainer = this.RuleRegistration.CreateRules();
                    this.Register(this.ruleContainer);
                }

                return this.ruleContainer;
            }
        }

        public IRuleRegistration RuleRegistration
        {
            get => this.ruleRegistration;
            set
            {
                // Enables tests that require custom rule registrations.
                // This allows the rule factory to be changed, and the next time the rules are accessed
                // the factory's creation method will generate a new set of rules.
                this.ruleRegistration = value;
                this.ruleContainer = value.CreateRules();
                this.Register(this.ruleContainer);
            }
        }

        /// <inheritdoc />
        public virtual Task InitializeAsync(ChainedHeader chainTip)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual void Dispose()
        {
        }

        /// <inheritdoc />
        private void Register(RuleContainer consensusRules)
        {
            this.SetupConsensusRules(consensusRules.HeaderValidationRules.Select(x => x as ConsensusRuleBase));
            this.SetupConsensusRules(consensusRules.IntegrityValidationRules.Select(x => x as ConsensusRuleBase));       
            this.SetupConsensusRules(consensusRules.PartialValidationRules.Select(x => x as ConsensusRuleBase));
            this.SetupConsensusRules(consensusRules.FullValidationRules.Select(x => x as ConsensusRuleBase));

            // Registers all rules that might be executed to the performance counter.
            this.performanceCounter = new ConsensusRulesPerformanceCounter(consensusRules);
        }

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

            this.ExecuteRules(this.RuleContainer.HeaderValidationRules, ruleContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public virtual ValidationContext IntegrityValidation(ChainedHeader header, Block block)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            this.ExecuteRules(this.RuleContainer.IntegrityValidationRules, ruleContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public virtual async Task<ValidationContext> FullValidationAsync(ChainedHeader header, Block block)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            await this.ExecuteRulesAsync(this.RuleContainer.FullValidationRules, ruleContext).ConfigureAwait(false);

            if (validationContext.Error != null)
                this.HandleConsensusError(validationContext);

            return validationContext;
        }

        /// <inheritdoc/>
        public virtual async Task<ValidationContext> PartialValidationAsync(ChainedHeader header, Block block)
        {
            Guard.NotNull(header, nameof(header));
            Guard.NotNull(block, nameof(block));

            var validationContext = new ValidationContext { BlockToValidate = block, ChainedHeaderToValidate = header };
            RuleContext ruleContext = this.CreateRuleContext(validationContext);

            await this.ExecuteRulesAsync(this.RuleContainer.PartialValidationRules, ruleContext).ConfigureAwait(false);

            if (validationContext.Error != null)
                this.HandleConsensusError(validationContext);

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

            this.logger.LogTrace("Marking '{0}' invalid.", hashToBan);
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
        public abstract Task<uint256> GetBlockHashAsync();

        /// <inheritdoc />
        public abstract Task<RewindState> RewindAsync();

        [NoTrace]
        public T GetRule<T>() where T : ConsensusRuleBase
        {
            object rule = this.RuleContainer.HeaderValidationRules.SingleOrDefault(r => r is T);

            if (rule == null)
                rule = this.RuleContainer.IntegrityValidationRules.SingleOrDefault(r => r is T);

            if (rule == null)
                rule = this.RuleContainer.PartialValidationRules.SingleOrDefault(r => r is T);

            if (rule == null)
                rule = this.RuleContainer.FullValidationRules.SingleOrDefault(r => r is T);

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
