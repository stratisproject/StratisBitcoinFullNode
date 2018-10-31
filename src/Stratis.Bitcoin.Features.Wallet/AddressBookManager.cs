using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class AddressBookManager : IAddressBookManager
    {
        /// <summary>File extension for wallet files.</summary>
        private const string AddressBookFileName = "addressbook.json";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An object capable of storing <see cref="Wallet"/>s to the file system.</summary>
        private readonly FileStorage<AddressBook> fileStorage;

        private AddressBook addressBook;

        public AddressBookManager(
            ILoggerFactory loggerFactory,
            DataFolder dataFolder)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(dataFolder, nameof(dataFolder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.fileStorage = new FileStorage<AddressBook>(dataFolder.RootPath);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // Create an address book file if none exists.
            if (this.fileStorage.Exists(AddressBookFileName))
            {
                this.addressBook = this.fileStorage.LoadByFileName(AddressBookFileName);

                this.logger.LogInformation("Address book was loaded.");
            }
            else
            {
                this.addressBook = new AddressBook();
                this.fileStorage.SaveToFile(this.addressBook, AddressBookFileName);

                this.logger.LogInformation("A new address book was created.");
            }
        }

        /// <inheritdoc />
        public AddressBookEntry AddNewAddress(string label, string address)
        {
            if (this.addressBook.Addresses.Any(i => i.Label == label || i.Address == address))
            {
                throw new AddressBookException($"An entry with label '{label}' or address '{address}' already exist in the address book.");
            }

            AddressBookEntry newEntry = new AddressBookEntry { Label = label, Address = address };

            this.addressBook.Addresses.Add(newEntry);
            this.fileStorage.SaveToFile(this.addressBook, AddressBookFileName);
            return newEntry;
        }

        /// <inheritdoc />
        public AddressBook GetAddressBook()
        {
            return this.addressBook;
        }

        /// <inheritdoc />
        public AddressBookEntry RemoveAddress(string label)
        {
            AddressBookEntry item = this.addressBook.Addresses.SingleOrDefault(i => i.Label == label);

            if (item == null)
            {
                return null;
            }

            this.addressBook.Addresses.Remove(item);
            this.fileStorage.SaveToFile(this.addressBook, AddressBookFileName);
            return item;
        }
    }
}
