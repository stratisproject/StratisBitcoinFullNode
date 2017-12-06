﻿using System;
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
        private readonly Dictionary<ConsensusRule, ConsensusRule> consensusRules;

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

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
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
