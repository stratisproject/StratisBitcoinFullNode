using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;


namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    public static class FederationGatewayRouteEndPoint
    {
        public const string ReceiveMaturedBlock = "receive-matured-block";
        public const string ReceiveCurrentBlockTip = "receive-current-block-tip";
        public const string ReSyncMaturedBlockDeposits = "resync_matured_block_depoits";
        public const string CreateSessionOnCounterChain = "create-session-oncounterchain";
        public const string ProcessSessionOnCounterChain = "process-session-oncounterchain";
    }

    /// <summary>
    /// API used to communicate across to the counter chain.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationGatewayController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILeaderProvider leaderProvider;

        private readonly ConcurrentChain chain;

        private readonly IMaturedBlockSender maturedBlockSender;

        private readonly IDepositExtractor depositExtractor;

        private readonly ILeaderReceiver leaderReceiver;

        public FederationGatewayController(
            ILoggerFactory loggerFactory,
            IMaturedBlockReceiver maturedBlockReceiver,
            ILeaderProvider leaderProvider,
            ConcurrentChain chain,
            IMaturedBlockSender maturedBlockSender,
            IDepositExtractor depositExtractor,
            ILeaderReceiver leaderReceiver)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.leaderProvider = leaderProvider;
            this.chain = chain;
            this.maturedBlockSender = maturedBlockSender;
            this.depositExtractor = depositExtractor;
            this.leaderReceiver = leaderReceiver;
        }

        [Route(FederationGatewayRouteEndPoint.ReceiveMaturedBlock)]
        [HttpPost]
        public void ReceiveMaturedBlock([FromBody] MaturedBlockDepositsModel maturedBlockDeposits)
        {
            this.maturedBlockReceiver.ReceiveMaturedBlockDeposits(maturedBlockDeposits);
        }

        /// <summary>
        /// Receives the current block tip to be used for updating the federated leader in a round robin fashion.
        /// </summary>
        /// <param name="blockTip"><see cref="BlockTipModelRequest"/>Block tip Hash and Height received.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(FederationGatewayRouteEndPoint.ReceiveCurrentBlockTip)]
        [HttpPost]
        public IActionResult ReceiveCurrentBlockTip([FromBody] BlockTipModelRequest blockTip)
        {
            Guard.NotNull(blockTip, nameof(blockTip));

            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.leaderProvider.Update(new BlockTipModel(uint256.Parse(blockTip.Hash), blockTip.Height));

                this.leaderReceiver.ReceiveLeader(this.leaderProvider);

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.ReceiveCurrentBlockTip, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not select the next federated leader: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Trigger a sync of missing matured blocks deposits.
        /// </summary>
        /// <param name="blockHashHeight">Last known Block tip Hash and Height.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(FederationGatewayRouteEndPoint.ReSyncMaturedBlockDeposits)]
        [HttpPost]
        public IActionResult ResyncMaturedBlockDeposits([FromBody] MaturedBlockModel blockHashHeight)
        {
            Guard.NotNull(blockHashHeight, nameof(blockHashHeight));

            if (!this.ModelState.IsValid)
            {
                IEnumerable<string> errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                ChainedHeader chainedHeader = this.chain.GetBlock(blockHashHeight.BlockHash);

                if (chainedHeader == null)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Block with hash {blockHashHeight.BlockHash} was not found on the block chain.", string.Empty);
                }

                int currentHeight = chainedHeader.Height;
                int matureHeight = (this.chain.Tip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

                if (currentHeight > matureHeight)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                        $"Block height {blockHashHeight.BlockHeight} submitted is not mature enough. Blocks less than a height of {matureHeight} can be processed.", string.Empty);
                }

                while (currentHeight < matureHeight)
                {
                    IMaturedBlockDeposits maturedBlockDeposits =
                        this.depositExtractor.ExtractMaturedBlockDeposits(chainedHeader);

                    if (maturedBlockDeposits == null) continue;

                    this.maturedBlockSender.SendMaturedBlockDepositsAsync(maturedBlockDeposits).ConfigureAwait(false);

                    currentHeight++;

                    chainedHeader = this.chain.GetBlock(currentHeight);
                }

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/{0}: {1}.", FederationGatewayRouteEndPoint.ReSyncMaturedBlockDeposits, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not re-sync matured block deposits: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(
                HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}
