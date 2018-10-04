using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Controllers.Converters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    [JsonConverter(typeof(ToStringJsonConverter))]
    public class NewAddressModel
    {
        public string Address { get; set; }

        public NewAddressModel(string address)
        {
            this.Address = address;
        }

        public override string ToString()
        {
            return this.Address;
        }
    }
}
