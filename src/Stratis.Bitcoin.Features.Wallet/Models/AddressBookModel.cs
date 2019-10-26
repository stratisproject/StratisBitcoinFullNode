using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// The model for the address book, usable by the API.
    /// </summary>
    public class AddressBookModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AddressBookModel"/> class.
        /// </summary>
        public AddressBookModel()
        {
            this.Addresses = new List<AddressBookEntryModel>();
        }

        /// <summary>
        /// A list of addresses.
        /// </summary>
        [JsonProperty(PropertyName = "addresses")]
        public IEnumerable<AddressBookEntryModel> Addresses { get; set; }
    }

    /// <summary>
    /// Represents an entry in the address book.
    /// </summary>
    public class AddressBookEntryModel
    {
        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// The label identifying this entry.
        /// </summary>
        [JsonProperty(PropertyName = "label")]
        public string Label { get; set; }
    }
}
