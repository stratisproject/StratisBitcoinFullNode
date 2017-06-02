using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Wallet.Models
{
    public class WalletBuildTransactionModel
    {		
		[JsonProperty(PropertyName = "fee")]
		public Money Fee { get; set; }
        
		[JsonProperty(PropertyName = "hex")]
		public string Hex { get; set; }

        [JsonProperty(PropertyName = "transactionId")]
        public uint256 TransactionId { get; set; }
    }   	
}
