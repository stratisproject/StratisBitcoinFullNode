using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Features.WatchOnlyWallet.Models;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a watch-only wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class WatchOnlyWalletController : Controller
    {
        /// <summary> The watch-only wallet manager. </summary>
        private readonly IWatchOnlyWalletManager watchOnlyWalletManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="WatchOnlyWalletController"/> class.
        /// </summary>
        /// <param name="watchOnlyWalletManager">The watch-only wallet manager.</param>
        public WatchOnlyWalletController(IWatchOnlyWalletManager watchOnlyWalletManager)
        {
            this.watchOnlyWalletManager = watchOnlyWalletManager;
        }

        /// <summary>
        /// Adds a base58 address to the watch list.
        /// </summary>
        /// <example>Request URL: /api/watchonlywallet/watch?address=mpK6g... </example>
        /// <param name="address">The base58 address to add to the watch list.</param>
        [Route("watch")]
        [HttpPost]
        public IActionResult Watch([FromQuery]string address)
        {
            // Checks the request is valid.
            if (string.IsNullOrEmpty(address))
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", "Address to watch is missing.");
            }

            try
            {
                this.watchOnlyWalletManager.WatchAddress(address);
                return this.Ok();
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the list of addresses being watched along with the transactions affecting them.
        /// </summary>        
        /// <example>Request URL: /api/watchonlywallet </example>
        /// <returns>The watch-only wallet or a collection of errors, if any.</returns>
        [HttpGet]
        public IActionResult GetWatchOnlyWallet()
        {
            try
            {
                // Map a watch-only wallet to a model object for display in the front end.
                var watchOnlyWallet = this.watchOnlyWalletManager.GetWatchOnlyWallet();
                WatchOnlyWalletModel model = new WatchOnlyWalletModel
                {
                    CoinType = watchOnlyWallet.CoinType,
                    Network = watchOnlyWallet.Network,
                    CreationTime = watchOnlyWallet.CreationTime
                };

                foreach (var watchAddress in watchOnlyWallet.WatchedAddresses)
                {
                    WatchedAddressModel watchedAddressModel = new WatchedAddressModel
                    {
                        Address = watchAddress.Value.Address,
                        Transactions = new List<TransactionVerboseModel>()
                    };

                    foreach (var transactionData in watchAddress.Value.Transactions)
                    {
                        watchedAddressModel.Transactions.Add(new TransactionVerboseModel(transactionData.Value.Transaction, watchOnlyWallet.Network));
                    }

                    model.WatchedAddresses.Add(watchedAddressModel);
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
