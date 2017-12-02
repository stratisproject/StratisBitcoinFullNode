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
        /// Register a new rule to the engine
        /// </summary>
        /// <typeparam name="TRule"></typeparam>
        /// <returns></returns>
        ConsensusRules Register<TRule>() where TRule : ConsensusRule, new();

        /// <summary>
        /// Register a new rule to the engine
        /// </summary>
        /// <param name="ruleRegistration">A container of rules to register.</param>
        /// <returns></returns>
        ConsensusRules Register(IRuleRegistration ruleRegistration);

        Task ExectueAsync(BlockValidationContext blockValidationContext);
    }

    public interface IRuleRegistration
    {
        IEnumerable<ConsensusRule> GetRules();
    }

    /// <inheritdoc />
    public class ConsensusRules : IConsensusRules
    {
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;


        private readonly Dictionary<ConsensusRule, ConsensusRule> consensusRules;

        public Network Network { get; }
        public IDateTimeProvider DateTimeProvider { get; }
        public ConcurrentChain Chain { get; }
        public NodeDeployments NodeDeployments { get; }

        public NBitcoin.Consensus ConsensusParams { get; }

        public ConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments)
        {
            this.Network = network;
            this.DateTimeProvider = dateTimeProvider;
            this.Chain = chain;
            this.NodeDeployments = nodeDeployments;
            this.loggerFactory = loggerFactory;
            this.ConsensusParams = this.Network.Consensus;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.consensusRules = new Dictionary<ConsensusRule, ConsensusRule>();
        }

        /// <inheritdoc />
        public ConsensusRules Register<TRule>() where TRule : ConsensusRule, new()
        {
            ConsensusRule rule = new TRule()
            {
                Parent = this,
                Logger = this.loggerFactory.CreateLogger(typeof(TRule).FullName)
            };

            foreach (var dependency in rule.Dependencies())
            {
                var depdendency = this.consensusRules.Keys.SingleOrDefault(w => w.GetType() == dependency);

                if (depdendency == null)
                {
                    throw new Exception(); // todo
                }
            }

            this.consensusRules.Add(rule, rule);

            return this;
        }

        /// <inheritdoc />
        public ConsensusRules Register(IRuleRegistration ruleRegistration)
        {
            foreach (var consensusRule in ruleRegistration.GetRules())
            {
                consensusRule.Parent = this;
                consensusRule.Logger = this.loggerFactory.CreateLogger(consensusRule.GetType().FullName);

                foreach (var dependency in consensusRule.Dependencies())
                {
                    var depdendency = this.consensusRules.Keys.SingleOrDefault(w => w.GetType() == dependency);

                    if (depdendency == null)
                    {
                        throw new Exception(); // todo
                    }
                }

                this.consensusRules.Add(consensusRule, consensusRule);
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
        public async Task ExectueAsync(BlockValidationContext blockValidationContext)
        {
            try
            {
                var context = new ContextInformation(blockValidationContext, this.ConsensusParams);
                blockValidationContext.Context = context;

                foreach (var consensusRule in this.consensusRules)
                {
                    var rule = consensusRule.Value;

                    if (context.BlockValidationContext.SkipValidation && rule.CanSkipValidation)
                    {
                        this.logger.LogTrace("Rule {0} skipped for block at height {1}.", nameof(rule), context.BlockValidationContext.ChainedBlock.Height);
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
    }
}
