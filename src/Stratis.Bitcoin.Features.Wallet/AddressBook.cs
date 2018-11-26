using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Represents an address book.
    /// </summary>
    public class AddressBook
    {
        /// <summary>
        /// Initializes a new instance of the wallet.
        /// </summary>
        public AddressBook()
        {
            this.Addresses = new List<AddressBookEntry>();
        }

        /// <summary>
        /// The list of addresses in the address book.
        /// </summary>
        [JsonProperty(PropertyName = "addresses")]
        public ICollection<AddressBookEntry> Addresses { get; set; }

    }

    /// <summary>
    /// Represents an entry in the address book.
    /// </summary>
    public class AddressBookEntry
    {
        /// <summary>
        /// A label uniquely identifying an entry.
        /// </summary>
        [JsonProperty(PropertyName = "label")]
        public string Label { get; set; }

        /// <summary>
        /// An address in base58.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }
    }
}
