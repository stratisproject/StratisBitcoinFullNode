using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Wallet.Models
{
	public class WalletModel
	{
		[JsonProperty(PropertyName = "network")]
		public string Network { get; set; }

		[JsonProperty(PropertyName = "fileName")]
		public string FileName { get; set; }

		[JsonProperty(PropertyName = "addresses")]
		public IEnumerable<string> Addresses { get; set; }
	}
}
