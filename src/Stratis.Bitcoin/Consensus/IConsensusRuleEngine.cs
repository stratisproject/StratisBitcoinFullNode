using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Primitives;

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
        /// <summary>
        /// Initialize the rules engine.
        /// </summary>
        /// <param name="chainTip">Last common header between chain repository and block store if it's available.
        Task InitializeAsync(ChainedHeader chainTip);

        /// <summary>
        /// Register a new rule to the engine
        /// </summary>
        ConsensusRuleEngine Register();

        /// <summary>
        /// Gets the consensus rule that is assignable to the supplied generic type.
        /// </summary>
        T GetRule<T>() where T : ConsensusRuleBase;

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

        /// <summary>Execute header validation rules.</summary>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        ValidationContext HeaderValidation(ChainedHeader header);

        /// <summary>Execute integrity validation rules.</summary>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <param name="block">The block that is going to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        ValidationContext IntegrityValidation(ChainedHeader header, Block block);

        /// <summary>Execute partial validation rules.</summary>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <param name="block">The block that is going to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        Task<ValidationContext> PartialValidationAsync(ChainedHeader header, Block block);

        /// <summary>Execute full validation rules.</summary>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <param name="block">The block that is going to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        Task<ValidationContext> FullValidationAsync(ChainedHeader header, Block block);
    }
}