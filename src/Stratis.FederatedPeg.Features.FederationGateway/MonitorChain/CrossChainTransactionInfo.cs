using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class CrossChainTransactionInfo
    {
        /// <summary>
        /// The hash of the source transaction that originates the fund transfer. 
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionHash { get; set; }

        /// <summary>
        /// The amount of the requested fund transfer.
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// The final destination of funds (on the counter chain).
        /// </summary>
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The block number where the source transaction resides.
        /// </summary>
        public int BlockNumber { get; set; }

        /// <summary>
        /// The hash of the block where the transaction resides.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// The hash of the destination transaction that moved the funds into the counterchain destination.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 CrossChainTransactionId { get; set; } = uint256.Zero;

        /// <summary>
        /// Helper to generate a json respresentation of this structure for logging/debugging.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}