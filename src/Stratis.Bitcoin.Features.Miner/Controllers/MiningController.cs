using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests.Controllers")]
namespace Stratis.Bitcoin.Features.Miner.Controllers
{
    /// <summary>
    /// API controller for calls related to PoW mining and PoS minting.
    /// </summary>
    [Route("api/[controller]")]
    public class MiningController : Controller
    {
        private const string ExceptionOccurredMessage = "Exception occurred: {0}";
        public const string LastPowBlockExceededMessage = "This is a POS node and mining is not allowed past block {0}";

        private readonly IConsensusManager consensusManager;
        private readonly ILogger logger;
        private readonly MiningFeature miningFeature;
        private readonly Network network;
        private readonly IPowMining powMining;
        private readonly IWalletManager walletManager;

        public MiningController(IConsensusManager consensusManager, IFullNode fullNode, ILoggerFactory loggerFactory, Network network, IPowMining powMining, IWalletManager walletManager)
        {
            Guard.NotNull(consensusManager, nameof(consensusManager));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(powMining, nameof(powMining));
            Guard.NotNull(walletManager, nameof(walletManager));

            this.consensusManager = consensusManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.miningFeature = fullNode.NodeFeature<MiningFeature>();
            this.network = network;
            this.powMining = powMining;
            this.walletManager = walletManager;
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
                if (this.network.Consensus.IsProofOfStake && (this.consensusManager.Tip.Height > this.network.Consensus.LastPOWBlock))
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed", string.Format(LastPowBlockExceededMessage, this.network.Consensus.LastPOWBlock));

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
                this.logger.LogError(ExceptionOccurredMessage, e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Stop mining.
        /// </summary>
        /// <param name="corsProtection">This body parameter is here to prevent a CORS call from triggering method execution.</param>
        /// <remarks>
        /// <seealso cref="https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS#Simple_requests"/>
        /// </remarks>
        [Route("stopmining")]
        [HttpPost]
        public IActionResult StopMining([FromBody] bool corsProtection = true)
        {
            try
            {
                this.miningFeature.StopMining();
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
        internal WalletAccountReference GetAccount()
        {
            const string noWalletMessage = "No wallet found";
            const string noAccountMessage = "No account found on wallet";
            

            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (walletName == null)
            {
                this.logger.LogError(ExceptionOccurredMessage, noWalletMessage);
                throw new Exception(noWalletMessage);
            }

            HdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();
            if (account == null)
            {
                this.logger.LogError(ExceptionOccurredMessage, noAccountMessage);
                throw new Exception(noAccountMessage);
            }

            var walletAccountReference = new WalletAccountReference(walletName, account.Name);
            return walletAccountReference;
        }
    }
}
