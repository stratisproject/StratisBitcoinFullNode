using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletSendTransactionToSidechainModel : WalletSendTransactionModel
    {
        [JsonProperty(PropertyName = "sidechainAddress")]
        public string SidechainAddressAddress { get; set; }
    }
}
