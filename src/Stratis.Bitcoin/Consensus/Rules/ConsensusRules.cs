using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Rules
{
    /// <inheritdoc />
    public abstract class ConsensusRules : IConsensusRules
    {
        /// <summary>A factory to creates logger instances for each rule.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>A collection of rules that well be executed by the rules engine.</summary>
        private readonly Dictionary<string, ConsensusRule> consensusRules;

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

        /// <inheritdoc />
        public ConsensusPerformanceCounter PerformanceCounter { get; }

        /// <summary>
        /// Group of rules that are marked with a <see cref="PartialValidationRuleAttribute"/> or no attribute.
        /// </summary>
        private readonly List<ConsensusRuleDescriptor> partialValidationRules;

        /// <summary>
        /// Group of rules that are marked with a <see cref="FullValidationRuleAttribute"/>.
        /// </summary>
        private readonly List<ConsensusRuleDescriptor> fullValidationRules;

        /// <summary>
        /// Group of rules that are marked with a <see cref="IntegrityValidationRuleAttribute"/>.
        /// </summary>
        private readonly List<ConsensusRuleDescriptor> integrityValidationRules;

        /// <summary>
        /// Group of rules that are marked with a <see cref="HeaderValidationRuleAttribute"/>.
        /// </summary>
        private readonly List<ConsensusRuleDescriptor> headerValidationRules;


        /// <inheritdoc />
        public IEnumerable<ConsensusRule> Rules => this.consensusRules.Values;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        protected ConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(nodeDeployments, nameof(nodeDeployments));
            Guard.NotNull(consensusSettings, nameof(consensusSettings));
            Guard.NotNull(checkpoints, nameof(checkpoints));

            this.Network = network;
            this.DateTimeProvider = dateTimeProvider;
            this.Chain = chain;
            this.NodeDeployments = nodeDeployments;
            this.loggerFactory = loggerFactory;
            this.ConsensusSettings = consensusSettings;
            this.Checkpoints = checkpoints;
            this.ConsensusParams = this.Network.Consensus;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.PerformanceCounter = new ConsensusPerformanceCounter(this.DateTimeProvider);

            this.consensusRules = new Dictionary<string, ConsensusRule>();
            this.partialValidationRules = new List<ConsensusRuleDescriptor>();
            this.headerValidationRules = new List<ConsensusRuleDescriptor>();
            this.fullValidationRules = new List<ConsensusRuleDescriptor>();
            this.integrityValidationRules = new List<ConsensusRuleDescriptor>();

        }

        /// <inheritdoc />
        public ConsensusRules Register(IRuleRegistration ruleRegistration)
        {
            Guard.NotNull(ruleRegistration, nameof(ruleRegistration));

            foreach (ConsensusRule consensusRule in ruleRegistration.GetRules())
            {
                consensusRule.Parent = this;
                consensusRule.Logger = this.loggerFactory.CreateLogger(consensusRule.GetType().FullName);
                consensusRule.Initialize();

                this.consensusRules.Add(consensusRule.GetType().FullName, consensusRule);

                List<RuleAttribute> ruleAttributes = Attribute.GetCustomAttributes(consensusRule.GetType()).OfType<RuleAttribute>().ToList();

                if(!ruleAttributes.Any())
                    throw new ConsensusException($"The rule {consensusRule.GetType().FullName} must have at least one {nameof(RuleAttribute)}");

                foreach (RuleAttribute ruleAttribute in ruleAttributes)
                {
                    if (ruleAttribute is FullValidationRuleAttribute)
                        this.fullValidationRules.Add(new ConsensusRuleDescriptor(consensusRule, ruleAttribute));

                    if (ruleAttribute is PartialValidationRuleAttribute)
                        this.partialValidationRules.Add(new ConsensusRuleDescriptor(consensusRule, ruleAttribute));

                    if (ruleAttribute is HeaderValidationRuleAttribute)
                        this.headerValidationRules.Add(new ConsensusRuleDescriptor(consensusRule, ruleAttribute));

                    if (ruleAttribute is IntegrityValidationRuleAttribute)
                        this.integrityValidationRules.Add(new ConsensusRuleDescriptor(consensusRule, ruleAttribute));
                }
            }

            return this;
        }

        public void SetErrorHandler(ConsensusRule errorHandler)
        {
            // TODO: set a rule that will be invoked when a validation of a block failed.

            // This will allow the creator of the blockchain to provide  
            // an error handler rule that is unique to the current blockchain.
            // future sidechains may have additional handling of errors that
            // extend or replace the default current error handling code.
        }

        /// <inheritdoc/>
        [Obsolete("Delete when CM activates")]
        public async Task AcceptBlockAsync(ValidationContext validationContext, ChainedHeader tip)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            validationContext.RuleContext = this.CreateRuleContext(validationContext, tip);

            try
            {
                await this.ValidateAndExecuteAsync(validationContext.RuleContext);
            }
            catch (ConsensusErrorException ex)
            {
                validationContext.Error = ex.ConsensusError;
            }
        }

        /// <inheritdoc />
        [Obsolete("Delete when CM activates")]
        public async Task ValidateAndExecuteAsync(RuleContext ruleContext)
        {
            await this.ValidateAsync(ruleContext);

            using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                foreach (ConsensusRuleDescriptor ruleDescriptor in this.fullValidationRules)
                {
                    await ruleDescriptor.Rule.RunAsync(ruleContext).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        [Obsolete("Delete when CM activates")]
        public async Task ValidateAsync(RuleContext ruleContext)
        {
            using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                foreach (ConsensusRuleDescriptor ruleDescriptor in this.partialValidationRules)
                {
                    if (ruleContext.SkipValidation && ruleDescriptor.RuleAttribute.CanSkipValidation)
                    {
                        this.logger.LogTrace("Rule {0} skipped for block at height {1}.", nameof(ruleDescriptor), ruleContext.ConsensusTip?.Height);
                    }
                    else
                    {
                        await ruleDescriptor.Rule.RunAsync(ruleContext).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void HeaderValidation(ValidationContext validationContext, ChainedHeader tip)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext, tip);

            this.ExecuteRules(this.headerValidationRules, ruleContext);
        }

        /// <inheritdoc/>
        public void IntegrityValidation(ValidationContext validationContext, ChainedHeader tip)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext, tip);

            this.ExecuteRules(this.integrityValidationRules, ruleContext);
        }

        /// <inheritdoc/>
        public async Task FullValidationAsync(ValidationContext validationContext, ChainedHeader tip)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext, tip);

            await this.ExecuteRulesAsync(this.partialValidationRules, ruleContext).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task PartialValidationAsync(ValidationContext validationContext, ChainedHeader tip)
        {
            Guard.NotNull(validationContext, nameof(validationContext));

            RuleContext ruleContext = this.CreateRuleContext(validationContext, tip);

            await this.ExecuteRulesAsync(this.partialValidationRules, ruleContext).ConfigureAwait(false);
        }

        private async Task ExecuteRulesAsync(List<ConsensusRuleDescriptor> rules, RuleContext ruleContext)
        {
            try
            {
                using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
                {
                    ruleContext.SkipValidation = ruleContext.ValidationContext.ChainedHeader.BlockValidationState == ValidationState.AssumedValid;

                    foreach (ConsensusRuleDescriptor ruleDescriptor in rules)
                    {
                        if (ruleContext.SkipValidation && ruleDescriptor.RuleAttribute.CanSkipValidation)
                        {
                            this.logger.LogTrace("Rule {0} skipped for block at height {1}.", nameof(ruleDescriptor), ruleContext.ConsensusTip?.Height);
                        }
                        else
                        {
                            await ruleDescriptor.Rule.RunAsync(ruleContext).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (ConsensusErrorException ex)
            {
                ruleContext.ValidationContext.Error = ex.ConsensusError;
            }
        }

        private void ExecuteRules(List<ConsensusRuleDescriptor> rules, RuleContext ruleContext)
        {
            try
            {
                using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
                {
                    ruleContext.SkipValidation = ruleContext.ValidationContext.ChainedHeader.BlockValidationState == ValidationState.AssumedValid;

                    foreach (ConsensusRuleDescriptor ruleDescriptor in rules)
                    {
                        if (ruleContext.SkipValidation && ruleDescriptor.RuleAttribute.CanSkipValidation)
                        {
                            this.logger.LogTrace("Rule {0} skipped for block at height {1}.", nameof(ruleDescriptor), ruleContext.ConsensusTip?.Height);
                        }
                        else
                        {
                            ruleDescriptor.Rule.Run(ruleContext);
                        }
                    }
                }
            }
            catch (ConsensusErrorException ex)
            {
                ruleContext.ValidationContext.Error = ex.ConsensusError;
            }
        }

        /// <inheritdoc />
        public abstract RuleContext CreateRuleContext(ValidationContext validationContext, ChainedHeader consensusTip);

        /// <inheritdoc />
        public abstract Task<uint256> GetBlockHashAsync();

        /// <inheritdoc />
        public abstract Task<RewindState> RewindAsync();

        /// <inheritdoc />
        public T GetRule<T>() where T : ConsensusRule
        {
            return (T)this.Rules.Single(r => r is T);
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
