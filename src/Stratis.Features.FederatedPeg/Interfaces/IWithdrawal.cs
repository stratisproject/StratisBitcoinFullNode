using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Represents a withdrawals made from a sidechain mutlisig, with the aim of realising
    /// a cross chain transfer.
    /// </summary>
    public interface IWithdrawal
    {
        /// <summary>
        /// The Id (or hash) of the source transaction that originates the fund transfer.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 DepositId { get; }

        /// <summary>
        /// The Id (or hash) of the source transaction that originated the fund
        /// transfer causing this withdrawal.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 Id { get; }

        /// <summary>
        /// The amount of fund transferred (in target currency).
        /// </summary>
        [JsonConverter(typeof(MoneyJsonConverter))]
        Money Amount { get; }

        /// <summary>
        /// The target address, on the target chain, for the fund deposited on the multisig.
        /// </summary>
        string TargetAddress { get; }

        /// <summary>
        /// The block number where the target deposit has been persisted.
        /// </summary>
        int BlockNumber { get; }

        /// <summary>
        /// The hash of the block where the target deposit has been persisted.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 BlockHash { get; }

        /// <summary>
        /// Abbreviated information about the withdrawal.
        /// </summary>
        string GetInfo();
    }
}