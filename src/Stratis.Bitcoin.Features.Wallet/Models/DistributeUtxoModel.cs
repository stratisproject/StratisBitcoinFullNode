using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class DistributeUtxoModel
    {
        [JsonProperty(PropertyName = "WalletName")]
        public string WalletName { get; set; }

        [JsonProperty(PropertyName = "UseUniqueAddressPerUtxo")]
        public bool UseUniqueAddressPerUtxo { get; set; }

        [JsonProperty(PropertyName = "UtxosCount")]
        public int UtxosCount { get; set; }

        [JsonProperty(PropertyName = "UtxoPerTransaction")]
        public int UtxoPerTransaction { get; set; }

        [JsonProperty(PropertyName = "TimestampDifferenceBetweenTransactions")]
        public int TimestampDifferenceBetweenTransactions { get; set; }

        [JsonProperty(PropertyName = "MinConfirmations")]
        public int MinConfirmations { get; set; }

        [JsonProperty(PropertyName = "DryRun")]
        public bool DryRun { get; set; }

        [JsonProperty(PropertyName = "WalletSendTransaction")]
        public List<WalletSendTransactionModel> WalletSendTransaction { get; set; }

        public DistributeUtxoModel()
        {
            this.WalletSendTransaction = new List<WalletSendTransactionModel>();
        }
    }
}
