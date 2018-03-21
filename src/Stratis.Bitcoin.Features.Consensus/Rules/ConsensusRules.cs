using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules
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

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        protected ConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints)
        {
            this.Network = network;
            this.DateTimeProvider = dateTimeProvider;
            this.Chain = chain;
            this.NodeDeployments = nodeDeployments;
            this.loggerFactory = loggerFactory;
            this.ConsensusSettings = consensusSettings;
            this.Checkpoints = checkpoints;
            this.ConsensusParams = this.Network.Consensus;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.consensusRules = new Dictionary<string, ConsensusRuleDescriptor>();
        }
        
        /// <inheritdoc />
        public IEnumerable<ConsensusRuleDescriptor> Rules => this.consensusRules.Values;

        /// <inheritdoc />
        public ConsensusRules Register(IRuleRegistration ruleRegistration)
        {
            foreach (var consensusRule in ruleRegistration.GetRules())
            {
                consensusRule.Parent = this;
                consensusRule.Logger = this.loggerFactory.CreateLogger(consensusRule.GetType().FullName);
                consensusRule.Initialize();
                
                this.consensusRules.Add(consensusRule.GetType().FullName, new ConsensusRuleDescriptor(consensusRule));
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

        /// <inheritdoc />
        public async Task ExecuteAsync(BlockValidationContext blockValidationContext)
        {
            Guard.NotNull(blockValidationContext, nameof(blockValidationContext));
            Guard.NotNull(blockValidationContext.RuleContext, nameof(blockValidationContext.RuleContext));

            try
            {
                var context = blockValidationContext.RuleContext;

                foreach (var consensusRule in this.consensusRules)
                {
                    var rule = consensusRule.Value;

                    if (context.SkipValidation && rule.CanSkipValidation)
                    {
                        this.logger.LogTrace("Rule {0} skipped for block at height {1}.", nameof(rule), blockValidationContext.ChainedBlock.Height);
                    }
                    else
                    {
                        await rule.Rule.RunAsync(context).ConfigureAwait(false);
                    }
                }
            }
            catch (ConsensusErrorException ex)
            {
                blockValidationContext.Error = ex.ConsensusError;
            }

            if (blockValidationContext.Error != null)
            {
                // TODO invoke the error handler rule.
            }
        }

        /// <inheritdoc />
        public async Task ValidateAsync(RuleContext ruleContext)
        {
            foreach (var consensusRule in this.consensusRules)
            {
                var rule = consensusRule.Value;

                if (rule.Attributes.Count == 0 || rule.Attributes.OfType<ValidationRuleAttribute>().Any())
                {
                    await rule.Rule.RunAsync(ruleContext).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Consensus rules that represent a Proof-Of-Work blockchain.
    /// </summary>
    public class PowConsensusRules : ConsensusRules
    {
        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public PowConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints)
        {
        }
    }

    /// <summary>
    /// Consensus rules that represent a Proof-Of-Stake blockchain.
    /// </summary>
    /// <remarks>
    /// A Proof-Of-Stake blockchain as implemented in this code base represents a hybrid POS/POW consensus model.
    /// </remarks>
    public class PosConsensusRules : ConsensusRules
    {
        /// <summary>Database of stake related data for the current blockchain.</summary>
        public IStakeChain StakeChain { get; }

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        public IStakeValidator StakeValidator { get; }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public PosConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, IStakeChain stakeChain, IStakeValidator stakeValidator) 
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints)
        {
            this.StakeChain = stakeChain;
            this.StakeValidator = stakeValidator;
        }
    }
}
