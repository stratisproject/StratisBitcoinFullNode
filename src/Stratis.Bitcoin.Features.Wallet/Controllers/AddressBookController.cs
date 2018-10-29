using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class AddressBookController : Controller
    {
        private readonly IAddressBookManager addressBookManager;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public AddressBookController(
            ILoggerFactory loggerFactory,
            IAddressBookManager addressBookManager)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(addressBookManager, nameof(addressBookManager));

            this.addressBookManager = addressBookManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Add an entry to the address book.
        /// </summary>
        /// <param name="request">The object containing the parameters used to create the wallet.</param>
        /// <returns>A JSON object containing the mnemonic created for the new wallet.</returns>
        [Route("address")]
        [HttpPost]
        public IActionResult AddAddress([FromBody]AddressBookEntryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                AddressBookEntry item = this.addressBookManager.AddNewAddress(request.Label, request.Address);

                return this.Json(new AddressBookEntryModel { Label = item.Label, Address = item.Address });
            }
            catch (AddressBookException e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem adding an address to the address book.", e.ToString());
            }
        }

        [Route("address")]
        [HttpDelete]
        public IActionResult RemoveAddress([FromQuery]string label)
        {
            Guard.NotEmpty(label, nameof(label));

            try
            {
                AddressBookEntry removedEntry = this.addressBookManager.RemoveAddress(label);

                if (removedEntry == null)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, $"No item with label '{label}' was found in the address book.", string.Empty);
                }

                return this.Json(new AddressBookEntryModel { Label = removedEntry.Label, Address = removedEntry.Address });
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem removing an address from the address book.", e.ToString());
            }
        }

        [Route("")]
        [HttpGet]
        public IActionResult GetAddressBook([FromQuery]int? skip, int? take)
        {
            try
            {
                AddressBook addressBook = this.addressBookManager.GetAddressBook();

                if (addressBook == null)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "No address book found.", string.Empty);
                }

                IEnumerable<AddressBookEntry> filteredAddressBook = addressBook.Addresses.Skip(skip ?? 0).Take(take ?? addressBook.Addresses.Count);

                AddressBookModel model = new AddressBookModel
                {
                    Addresses = filteredAddressBook.Select(res => new AddressBookEntryModel { Label = res.Label, Address = res.Address })
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem getting the address book.", e.ToString());
            }
        }
    }
}
