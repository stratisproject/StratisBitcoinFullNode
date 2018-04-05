using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.FederatedSidechainWallet.Models
{
    public class WalletSendTransactionModel : Wallet.Models.WalletSendTransactionModel
    {
        [JsonProperty(PropertyName = "sidechainAddress")]
        public string SidechainAddress { get; set; }
    }
}
