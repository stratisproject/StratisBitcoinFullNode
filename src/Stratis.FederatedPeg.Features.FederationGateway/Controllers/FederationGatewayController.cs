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
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;


namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    /// <summary>
    /// API used to communicate across to the counter chain.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationGatewayController : Controller
    {
        public const string ReceiveMaturedBlockRoute = "receive-matured-block";

        public const string ReceiveCurrentBlockTipRoute = "receive-current-block-tip";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly ICounterChainSessionManager counterChainSessionManager;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILeaderProvider leaderProvider;

        public FederationGatewayController(
            ILoggerFactory loggerFactory,
            ICounterChainSessionManager counterChainSessionManager,
            IMaturedBlockReceiver maturedBlockReceiver,
            ILeaderProvider leaderProvider)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.counterChainSessionManager = counterChainSessionManager;
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.leaderProvider = leaderProvider;
        }

        [Route(ReceiveMaturedBlockRoute)]
        [HttpPost]
        public void ReceiveMaturedBlock([FromBody] MaturedBlockDepositsModel maturedBlockDeposits)
        {
            this.maturedBlockReceiver.ReceiveMaturedBlockDeposits(maturedBlockDeposits);
        }

        /// <summary>
        /// Our deposit and withdrawal transactions start on mainchain and sidechain respectively. Two transactions are used, one on each chain, to complete
        /// the 'movement'.
        /// This API call informs the counterchain node that this session exists.  All the federation nodes monitoring the blockchain will ask
        /// their counterchains so register the session.  The boss counterchain will use this session to process the transaction whereas the other nodes
        /// will use this session information to Verify that the transaction is valid.
        /// </summary>
        /// <param name="createCounterChainSessionRequest">Used to pass the SessionId, Amount and Destination address to the counter chain.</param>
        /// <returns>An ActionResult.</returns>
        [Route("create-session-oncounterchain")]
        [HttpPost]
        public IActionResult CreateSessionOnCounterChain([FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest)
        {
            Guard.NotNull(createCounterChainSessionRequest, nameof(createCounterChainSessionRequest));

            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(createCounterChainSessionRequest.BlockHeight), createCounterChainSessionRequest.BlockHeight, "Transactions Count", createCounterChainSessionRequest.CounterChainTransactionInfos.Count);

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.counterChainSessionManager.CreateSessionOnCounterChain(createCounterChainSessionRequest.BlockHeight, createCounterChainSessionRequest.CounterChainTransactionInfos);
                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/create-session-oncounterchain: {0}.", e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not create session on counter chain: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// The session boss asks his counterchain node to go ahead with broadcasting the partial template, wait for replies, then build and broadcast
        /// the counterchain transaction. (Other federation counterchain nodes will not do this unless they later become the boss however the non-boss 
        /// counterchain nodes will know about the session already and can verify the transaction against their session info.)
        /// <param name="createCounterChainSessionRequest">Used to pass the SessionId, Amount and Destination address to the counter chain.</param>
        /// <returns>An ActionResult.</returns>
        [Route("process-session-oncounterchain")]
        [HttpPost]
        public async Task<IActionResult> ProcessSessionOnCounterChain([FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest)
        {
            Guard.NotNull(createCounterChainSessionRequest, nameof(createCounterChainSessionRequest));

            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(createCounterChainSessionRequest.BlockHeight), createCounterChainSessionRequest.BlockHeight, "Transactions Count", createCounterChainSessionRequest.CounterChainTransactionInfos.Count);
            
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = await this.counterChainSessionManager.ProcessCounterChainSession(createCounterChainSessionRequest.BlockHeight);
                
                return this.Json(result);
            }
            catch (InvalidOperationException e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/process-session-oncounterchain: {0}.", e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, $"Could not create partial transaction session: {e.Message}", e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/process-session-oncounterchain: {0}.", e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not create partial transaction session: {e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Receives the current block tip to be used for updating the federated leader in a round robin fashion.
        /// </summary>
        /// <param name="blockTip"><see cref="BlockTipModelRequest"/>Block tip Hash and Height received.</param>
        /// <returns><see cref="IActionResult"/>OK on success.</returns>
        [Route(ReceiveCurrentBlockTipRoute)]
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

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception thrown calling /api/FederationGateway/{0}: {1}.", ReceiveCurrentBlockTipRoute, e.Message);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not select the next federated leader: {e.Message}", e.ToString());
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
