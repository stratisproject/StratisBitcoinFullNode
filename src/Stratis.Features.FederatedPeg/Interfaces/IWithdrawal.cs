using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Represents a withdrawal made from a source chain to a target chain.
    /// </summary>
    public interface IWithdrawal
    {
        /// <summary>
        /// The hash of the deposit transaction from the source chain.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 DepositId { get; }

        /// <summary>
        /// The hash of the withdrawal transaction to the target chain.
        /// This can be null until the trx is fully signed.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 Id { get; }

        /// <summary>
        /// The amount of fund transferred (in target currency).
        /// </summary>
        [JsonConverter(typeof(MoneyJsonConverter))]
        Money Amount { get; }

        /// <summary>
        /// The target address, on the target chain, for the fund deposited on the multi-sig.
        /// </summary>
        string TargetAddress { get; }

        /// <summary>
        /// The block number on the target chain where the withdrawal has been deposited.
        /// </summary>
        int BlockNumber { get; }

        /// <summary>
        /// The block hash on the target chain where the withdrawal has been deposited.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 BlockHash { get; }

        /// <summary>
        /// Abbreviated information about the withdrawal.
        /// </summary>
        string GetInfo();
    }
}
