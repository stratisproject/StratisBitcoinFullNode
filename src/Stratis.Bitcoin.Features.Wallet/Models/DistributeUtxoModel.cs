using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class DistributeUtxoModel
    {
        [JsonProperty(PropertyName = "WalletSendTransaction")]
        public List<WalletSendTransactionModel> WalletSendTransaction { get; set; }

        public DistributeUtxoModel()
        {
            this.WalletSendTransaction = new List<WalletSendTransactionModel>();
        }
    }
}
