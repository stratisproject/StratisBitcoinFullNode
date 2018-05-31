using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.MonitorChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    /// <summary>
    /// API used to communicate across to the counter chain.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationGatewayController : Controller
    {
        private ICounterChainSessionManager counterChainSessionManager;

        private IMonitorChainSessionManager monitorChainSessionManager;

        public FederationGatewayController(ICounterChainSessionManager counterChainSessionManager,
            IMonitorChainSessionManager monitorChainSessionManager)
        {
            this.monitorChainSessionManager = monitorChainSessionManager;
            this.counterChainSessionManager = counterChainSessionManager;
        }

        [Route("create-session-oncounterchain")]
        [HttpPost]
        public IActionResult CreateSessionOnCounterChain([FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest)
        {
            Guard.NotNull(createCounterChainSessionRequest, nameof(createCounterChainSessionRequest));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.counterChainSessionManager.CreateSessionOnCounterChain(
                    createCounterChainSessionRequest.SessionId,
                    createCounterChainSessionRequest.Amount,
                    createCounterChainSessionRequest.DestinationAddress);
                return this.Ok();
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not initialize sidechain:{e.Message}", e.ToString());
            }
        }

        /// <summary>
        /// Our deposit and withdrawal transactions start on mainchain and sidechain respectively. Two transactions are used, one on each chain, to complete
        /// the 'movement'.  This API call asks the counter chain to action it's transaction.
        /// </summary>
        /// <param name="createCounterChainSessionRequest">Used to pass the SessionId, Amount and Destination address to the counter chain.</param>
        /// <returns>An ActionResult.</returns>
        [Route("request-counter-completion")]
        [HttpPost]
        public async Task<IActionResult> CreatePartialTransactionSession([FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest)
        {
            Guard.NotNull(createCounterChainSessionRequest, nameof(createCounterChainSessionRequest));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.counterChainSessionManager.ProcessCounterChainSession(
                    createCounterChainSessionRequest.SessionId,
                    createCounterChainSessionRequest.Amount,
                    createCounterChainSessionRequest.DestinationAddress);
                return this.Json(uint256.Zero); //todo: this is temp.
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not initialize sidechain:{e.Message}", e.ToString());
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
