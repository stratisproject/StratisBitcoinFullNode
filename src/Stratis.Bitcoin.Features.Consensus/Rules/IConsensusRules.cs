﻿using System.Collections.Generic;
using System.Threading.Tasks;

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
}