using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.Miner.Controllers
{
    /// <summary>
    /// API controller for calls related to PoW mining and PoS minting.
    /// </summary>
    [Route("api/[controller]")]
    public class MiningController : Controller
    {
        private const string exceptionOccurredMessage = "Exception occurred: {0}";
        private readonly IFullNode fullNode;
        private readonly ILogger logger;

        public MiningController(IFullNode fullNode)
        {
            Guard.NotNull(fullNode, nameof(fullNode));

            this.fullNode = fullNode;
            this.logger = fullNode.NodeService<ILoggerFactory>().CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Tries to mine one or more blocks.
        /// </summary>
        /// <param name="request">Number of blocks to mine.</param>
        /// <returns>List of block header hashes of newly mined blocks.</returns>
        /// <remarks>It is possible that less than the required number of blocks will be mined because the generating function only
        /// tries all possible header nonces values.</remarks>
        [Route("startmining")]
        [HttpPost]
        public IActionResult StartMining([FromBody]MiningRequest request)
        {
            Guard.NotNull(request, nameof(request));

            try
            {
                if (this.fullNode.Network.Consensus.IsProofOfStake &&
                    this.fullNode.NodeService<IConsensusManager>(false).Tip.Height > this.fullNode.Network.Consensus.LastPOWBlock)
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed", string.Format("This is a POS node and it's consensus tip is higher that the allowed last POW block height of {0}", this.fullNode.Network.Consensus.LastPOWBlock));

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
                HdAddress address = this.fullNode.NodeService<IWalletManager>().GetUnusedAddress(accountReference);

                var generateBlocksModel = new GenerateBlocksModel
                {
                    Blocks = this.fullNode.NodeService<IPowMining>().GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)blockCount, int.MaxValue)
                };

                this.logger.LogTrace("(-):*.{0}={1}", "Generated block count", generateBlocksModel.Blocks.Count);

                return this.Json(generateBlocksModel);
            }
            catch (Exception e)
            {
                this.logger.LogError(exceptionOccurredMessage, e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("stopmining")]
        [HttpPost]
        public IActionResult StopMining()
        {
            try
            {
                this.fullNode.NodeFeature<MiningFeature>().StopMining();
                return this.Ok();
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
            const string noWalletMessage = "No wallet found";
            const string noAccountMessage = "No account found on wallet";

            this.logger.LogTrace("()");

            string walletName = this.fullNode.NodeService<IWalletManager>().GetWalletsNames().FirstOrDefault();
            if (walletName == null)
            {
                this.logger.LogError(exceptionOccurredMessage, noWalletMessage);
                throw new Exception(noWalletMessage);
            }

            HdAccount account = this.fullNode.NodeService<IWalletManager>().GetAccounts(walletName).FirstOrDefault();
            if (account == null)
            {
                this.logger.LogError(exceptionOccurredMessage, noAccountMessage);
                throw new Exception(noAccountMessage);
            }

            var walletAccountReference = new WalletAccountReference(walletName, account.Name);

            this.logger.LogTrace("(-):'{0}'", walletAccountReference);
            return walletAccountReference;
        }
    }
}
