using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
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
        private readonly Dictionary<string, ConsensusRuleDescriptor> consensusRules;

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
        /// Grouping of rules that are marked with a <see cref="ValidationRuleAttribute"/> or no attribute.
        /// </summary>
        private readonly List<ConsensusRuleDescriptor> validationRules;

        /// <summary>
        /// Grouping of rules that are marked with a <see cref="ExecutionRuleAttribute"/>.
        /// </summary>
        private readonly List<ConsensusRuleDescriptor> executionRules;

        /// <inheritdoc />
        public IEnumerable<ConsensusRuleDescriptor> Rules => this.consensusRules.Values;

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

            this.consensusRules = new Dictionary<string, ConsensusRuleDescriptor>();
            this.validationRules = new List<ConsensusRuleDescriptor>();
            this.executionRules = new List<ConsensusRuleDescriptor>();
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
                
                this.consensusRules.Add(consensusRule.GetType().FullName, new ConsensusRuleDescriptor(consensusRule));
            }

            this.validationRules.AddRange(this.consensusRules.Values.Where(w => w.RuleAttributes.OfType<ValidationRuleAttribute>().Any() || w.RuleAttributes.Count == 0));
            this.executionRules.AddRange(this.consensusRules.Values.Where(w => w.RuleAttributes.OfType<ExecutionRuleAttribute>().Any()));

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
        public async Task ValidateAndExecuteAsync(RuleContext ruleContext)
        {
            await this.ValidateAsync(ruleContext);

            using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                foreach (ConsensusRuleDescriptor ruleDescriptor in this.executionRules)
                {
                    await ruleDescriptor.Rule.RunAsync(ruleContext).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task ValidateAsync(RuleContext ruleContext)
        {
            using (new StopwatchDisposable(o => this.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                foreach (ConsensusRuleDescriptor ruleDescriptor in this.validationRules)
                {
                    if (ruleContext.SkipValidation && ruleDescriptor.CanSkipValidation)
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

        /// <inheritdoc />
        public abstract RuleContext CreateRuleContext(ValidationContext validationContext, ChainedHeader consensusTip);

        /// <inheritdoc />
        public abstract Task<uint256> GetBlockHashAsync();

        /// <inheritdoc />
        public abstract Task<RewindState> RewindAsync();

        /// <inheritdoc />
        public T GetRule<T>() where T : ConsensusRule
        {
            return (T)this.Rules.Single(r => r.Rule is T).Rule;
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
