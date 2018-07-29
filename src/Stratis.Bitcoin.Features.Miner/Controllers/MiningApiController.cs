using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.Miner.Controllers
{
    /// <summary>
    /// RPC controller for calls related to PoW mining and PoS minting.
    /// </summary>
    [Route("api/[controller]")]
    public class MiningApiController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>PoW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>Wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="powMining">PoW miner.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="walletManager">The wallet manager.</param>
        public MiningApiController(IPowMining powMining, ILoggerFactory loggerFactory, IWalletManager walletManager) 
        {
            Guard.NotNull(powMining, nameof(powMining));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletManager = walletManager;
            this.powMining = powMining;
       }

        /// <summary>
        /// Tries to mine one or more blocks.
        /// </summary>
        /// <param name="request">Number of blocks to mine.</param>
        /// <returns>List of block header hashes of newly mined blocks.</returns>
        /// <remarks>It is possible that less than the required number of blocks will be mined because the generating function only
        /// tries all possible header nonces values.</remarks>
        [Route("generate")]
        [HttpPost]
        public IActionResult Generate([FromBody]MiningRequest request)
        {
            Guard.NotNull(request, nameof(request));

            try
            {
                if (!this.ModelState.IsValid)
                {
                    IEnumerable<string> errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
                }

                int blockCount = request.BlockCount;

                if (blockCount <= 0)
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Invalid request", "The number of blocks to mine must be higher than zero.");

                this.logger.LogTrace("({0}:{1})", nameof(request.BlockCount), blockCount);

                WalletAccountReference accountReference = this.GetAccount();
                HdAddress address = this.walletManager.GetUnusedAddress(accountReference);

                var generateBlocksModel = new GenerateBlocksModel
                {
                    Blocks = this.powMining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)blockCount, int.MaxValue)
                };

                this.logger.LogTrace("(-):*.{0}={1}", "Generated block count", generateBlocksModel.Blocks.Count);

                return this.Json(generateBlocksModel);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
   
        /// <summary>
        /// Finds first available wallet and its account.
        /// </summary>
        /// <returns>Reference to wallet account.</returns>
        private WalletAccountReference GetAccount()
        {
            this.logger.LogTrace("()");

            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (walletName == null)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");

            HdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();
            if (account == null)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "No account found on wallet");

            var res = new WalletAccountReference(walletName, account.Name);

            this.logger.LogTrace("(-):'{0}'", res);
            return res;
        }
    }
}
