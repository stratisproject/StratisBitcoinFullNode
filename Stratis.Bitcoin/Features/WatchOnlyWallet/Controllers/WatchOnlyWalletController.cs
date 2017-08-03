using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class WatchOnlyWalletController : Controller
    {
        private readonly IWatchOnlyWalletManager watchOnlyWalletManager;

        public WatchOnlyWalletController(IWatchOnlyWalletManager watchOnlyWalletManager)
        {
            this.watchOnlyWalletManager = watchOnlyWalletManager;
        }

        /// <summary>
        /// Adds a script to the watch list.
        /// </summary>
        /// <param name="script">The script pubkey to add to the watch list.</param>
        [Route("watch")]
        [HttpPost]
        public IActionResult Watch([FromQuery]string script)
        {
            // checks the request is valid
            if (string.IsNullOrEmpty(script))
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", "Script to watch is missing.");
            }

            try
            {
                this.watchOnlyWalletManager.Watch(new Script(script));
                return this.Ok();
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the watch list.
        /// </summary>
        /// <returns>The watch-only wallet or a collection of errors, if any.</returns>
        [HttpGet]
        public IActionResult GetWallet()
        {
            try
            {
                return this.Json(this.watchOnlyWalletManager.GetWallet());
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
