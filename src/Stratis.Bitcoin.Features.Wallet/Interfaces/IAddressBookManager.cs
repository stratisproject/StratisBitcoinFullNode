namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    /// <summary>
    /// An interface providing operations on an address book.
    /// </summary>
    public interface IAddressBookManager
    {
        /// <summary>
        /// Initializes the address book manager.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Get the address book.
        /// </summary>
        /// <returns>The address book.</returns>
        AddressBook GetAddressBook();

        /// <summary>
        /// Add a new entry to the address book.
        /// </summary>
        /// <param name="label">A label uniquely identifying the entry.</param>
        /// <param name="address">The address.</param>
        /// <returns>The newly added entry in the address book.</returns>
        AddressBookEntry AddNewAddress(string label, string address);

        /// <summary>
        /// Remove an entry from the address book.
        /// </summary>
        /// <param name="label">A label uniquely identifying the entry to remove.</param>
        /// <returns>The entry removed from the address book.</returns>
        AddressBookEntry RemoveAddress(string label);
    }
}