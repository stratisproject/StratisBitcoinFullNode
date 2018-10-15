using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>Transaction details model for RPC method gettransaction.</summary>
    public class GetTransactionDetailsModel
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("amount")]
        public Money Amount { get; set; }
    }

    /// <summary>Model for RPC method gettransaction.</summary>
    public class GetTransactionModel
    {
        [JsonProperty("amount")]
        public Money Amount { get; set; }

        [JsonProperty("blockhash")]
        public uint256 BlockHash { get; set; }

        [JsonProperty("txid")]
        public uint256 TransactionId { get; set; }

        [JsonProperty("time")]
        public long? TransactionTime { get; set; }

        [JsonProperty("details")]
        public List<GetTransactionDetailsModel> Details { get; set; }

        [JsonProperty("hex")]
        public string Hex { get; set; }
    }
}
