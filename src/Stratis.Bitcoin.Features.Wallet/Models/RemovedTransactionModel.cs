using System;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// Class containing details of a transaction successfully removed from the wallet.
    /// </summary>
    public class RemovedTransactionModel
    {
        [JsonProperty(PropertyName = "transactionId")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }

        [JsonProperty(PropertyName = "creationTime")]
        public DateTimeOffset CreationTime { get; set; }
    }
}
