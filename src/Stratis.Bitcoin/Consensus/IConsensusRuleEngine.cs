using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// An engine that enforce the execution and validation of consensus rule.
    /// </summary>
    /// <remarks>
    /// In order for a block to be valid it has to successfully pass the rules checks.
    /// A block  that is not valid will result in the <see cref="ValidationContext.Error"/> as not <c>null</c>.
    /// </remarks>
    public interface IConsensusRuleEngine : IDisposable
    {
        ////TODO ACTIVATION
        ///// <summary>
        ///// Collection of all the rules that are registered with the engine.
        ///// </summary>
        //List<ConsensusRule> Rules { get; }

        /// <summary>
        /// Keeps track of how much time different actions took to execute and how many times they were executed.
        /// </summary>
        ConsensusPerformanceCounter PerformanceCounter { get; }

        /// <summary>
        /// Initialize the rules engine.
        /// </summary>
        Task Initialize();

        /// <summary>
        /// Register a new rule to the engine
        /// </summary>
        ConsensusRuleEngine Register();

        //TODO ACTIVATION
        /// <summary>
        /// Gets the consensus rule that is assignable to the supplied generic type.
        /// </summary>
        //T GetRule<T>() where T : ConsensusRule;

        /// <summary>
        /// Create an instance of the <see cref="RuleContext"/> to be used by consensus validation.
        /// </summary>
        /// <remarks>
        /// Each network type can specify it's own <see cref="RuleContext"/>.
        /// </remarks>
        RuleContext CreateRuleContext(ValidationContext validationContext);

        /// <summary>
        /// Retrieves the block hash of the current tip of the chain.
        /// </summary>
        /// <returns>Block hash of the current tip of the chain.</returns>
        Task<uint256> GetBlockHashAsync();

        /// <summary>
        /// Rewinds the chain to the last saved state.
        /// <para>
        /// This operation includes removing the recent transactions
        /// and restoring the chain to an earlier state.
        /// </para>
        /// </summary>
        /// <returns>Hash of the block header which is now the tip of the chain.</returns>
        Task<RewindState> RewindAsync();

        /// <summary>
        /// Execute rules that are marked with the <see cref="HeaderValidationRuleAttribute"/>.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        void HeaderValidation(ValidationContext validationContext);

        /// <summary>
        /// Execute rules that are marked with the <see cref="IntegrityValidationRuleAttribute"/>.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        void IntegrityValidation(ValidationContext validationContext);

        /// <summary>
        /// Execute rules that are marked with the <see cref="PartialValidationRuleAttribute"/>.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        Task PartialValidationAsync(ValidationContext validationContext);

        /// <summary>
        /// Execute rules that are marked with the <see cref="FullValidationRuleAttribute"/>.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        Task FullValidationAsync(ValidationContext validationContext);
    }
}