using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    /// <summary>
    /// An engine that enforce the execution and validation of consensus rule. 
    /// </summary>
    /// <remarks>
    /// In order for a block to be valid it has to successfully pass the rules checks.
    /// A block  that is not valid will result in the <see cref="BlockValidationContext.Error"/> as not <c>null</c>.
    /// </remarks>
    public interface IConsensusRules
    {
        /// <summary>
        /// Collection of all the rules that are registered with the engine.
        /// </summary>
        IEnumerable<ConsensusRule> Rules { get; }

        /// <summary>
        /// Register a new rule to the engine
        /// </summary>
        /// <param name="ruleRegistration">A container of rules to register.</param>
        /// <returns></returns>
        ConsensusRules Register(IRuleRegistration ruleRegistration);

        /// <summary>
        /// Execute the consensus rule engine.
        /// </summary>
        /// <param name="blockValidationContext">A context that holds information about the current validated block.</param>
        /// <returns>The processing task.</returns>
        Task ExecuteAsync(BlockValidationContext blockValidationContext);

        /// <summary>
        /// Execute the consensus rule engine in validation mode only, no state will be changed.
        /// </summary>
        /// <param name="ruleContext">A context that holds information about the current validated block.</param>
        /// <returns>The processing task.</returns>
        Task ValidateAsync(RuleContext ruleContext);
    }

    /// <summary>
    /// An interface that will allow the registration of bulk consensus rules in to the engine.
    /// </summary>
    public interface IRuleRegistration
    {
        /// <summary>
        /// The rules that will be registered with the rules engine.
        /// </summary>
        /// <returns>A list of rules.</returns>
        IEnumerable<ConsensusRule> GetRules();
    }

    /// <inheritdoc />
    public class ConsensusRules : IConsensusRules
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

        /// <summary>The main loop of the consensus execution engine.</summary>
        public ConsensusLoop ConsensusLoop { get; set; }

        /// <summary>Consensus settings for the full node.</summary>
        public ConsensusSettings ConsensusSettings { get; }

        /// <summary>Provider of block header hash checkpoints.</summary>
        public ICheckpoints Checkpoints { get; }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public ConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints)
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
            this.consensusRules = new Dictionary<string, ConsensusRule>();
        }
        
        /// <inheritdoc />
        public IEnumerable<ConsensusRule> Rules => this.consensusRules.Values;

        /// <inheritdoc />
        public ConsensusRules Register(IRuleRegistration ruleRegistration)
        {
            foreach (var consensusRule in ruleRegistration.GetRules())
            {
                consensusRule.Parent = this;
                consensusRule.Logger = this.loggerFactory.CreateLogger(consensusRule.GetType().FullName);
                consensusRule.Initialize();
                
                this.consensusRules.Add(consensusRule.GetType().FullName, consensusRule);
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

                    if (context.SkipValidation && rule.ValidationRule)
                    {
                        this.logger.LogTrace("Rule {0} skipped for block at height {1}.", nameof(rule), blockValidationContext.ChainedBlock.Height);
                    }
                    else
                    {
                        await rule.RunAsync(context).ConfigureAwait(false);
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

                if (rule.ValidationRule)
                {
                    await rule.RunAsync(ruleContext).ConfigureAwait(false);
                }
            }
        }
    }

    public static class ConsensusRulesExtension
    {
        /// <summary>
        /// Try to find a consensus rule in the rules collection.
        /// </summary>
        /// <typeparam name="T">The type of rule to find.</typeparam>
        /// <param name="rules">The rules to look in.</param>
        /// <returns>The rule or <c>null</c> if not found in the list.</returns>
        public static T TryFindRule<T>(this IEnumerable<ConsensusRule> rules) where T : ConsensusRule
        {
            return rules.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Find a consensus rule in the rules collection or throw an exception of not found.
        /// </summary>
        /// <typeparam name="T">The type of rule to find.</typeparam>
        /// <param name="rules">The rules to look in.</param>
        /// <returns>The rule or <c>null</c> if not found in the list.</returns>
        public static T FindRule<T>(this IEnumerable<ConsensusRule> rules) where T : ConsensusRule
        {
            T rule = rules.TryFindRule<T>();

            if (rule == null)
            {
                throw new Exception(); // todo:
            }

            return rule;
        }
    }
}
