using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// Class containing details of a wallet stats model.
    /// </summary>
    public class WalletStatsModel
    {
        [JsonProperty(PropertyName = "WalletName")]
        public string WalletName { get; set; }

        [JsonProperty(PropertyName = "TotalUtxoCount")]
        public int TotalUtxoCount { get; set; }

        [JsonProperty(PropertyName = "UniqueTransactionCount")]
        public int UniqueTransactionCount { get; set; }

        [JsonProperty(PropertyName = "UniqueBlockCount")]
        public int UniqueBlockCount { get; set; }

        [JsonProperty(PropertyName = "CountOfTransactionsWithAtLeastMaxReorgConfirmations")]
        public int FinalizedTransactions { get; set; }

        [JsonProperty(PropertyName = "UtxoAmounts")]
        public List<UtxoAmountModel> UtxoAmounts { get; set; }

        [JsonProperty(PropertyName = "UtxoPerTransaction")]
        public List<UtxoPerTransactionModel> UtxoPerTransaction { get; set; }

        [JsonProperty(PropertyName = "UtxoPerBlock")]
        public List<UtxoPerBlockModel> UtxoPerBlock { get; set; }
    }

    public class UtxoAmountModel
    {
        [JsonProperty(PropertyName = "Amount")]
        public decimal Amount { get; set; }

        [JsonProperty(PropertyName = "Count")]
        public int Count { get; set; }
    }

    public class UtxoPerTransactionModel
    {
        [JsonProperty(PropertyName = "UtxoPerTransaction")]
        public int WalletInputsPerTransaction { get; set; }

        [JsonProperty(PropertyName = "Count")]
        public int Count { get; set; }
    }

    public class UtxoPerBlockModel
    {
        [JsonProperty(PropertyName = "UtxoPerBlock")]
        public int WalletInputsPerBlock { get; set; }

        [JsonProperty(PropertyName = "Count")]
        public int Count { get; set; }
    }
}