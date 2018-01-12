using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// A model representing a list of addresses the user has in a wallet account.
    /// </summary>
    public class AddressesModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AddressesModel"/> class.
        /// </summary>
        public AddressesModel()
        {
            this.Addresses = new List<AddressModel>();
        }
        
        /// <summary>
        /// A list of addresses.
        /// </summary>
        [JsonProperty(PropertyName = "addresses")]
        public IEnumerable<AddressModel> Addresses { get; set; }
    }

    /// <summary>
    /// Represents an address a user has in their wallet.
    /// </summary>
    public class AddressModel
    {
        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// A value indicating whether this address has been used in a transaction.
        /// </summary>
        [JsonProperty(PropertyName = "isUsed")]
        public bool IsUsed { get; set; }

        /// <summary>
        /// A value indicating whether this address is a change address.
        /// </summary>
        [JsonProperty(PropertyName = "isChange")]
        public bool IsChange { get; set; }
    }
}
