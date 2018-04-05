using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.MainchainGeneratorServices.Models;

namespace Stratis.FederatedPeg.Features.MainchainGeneratorServices
{
    /// <summary>
    /// Controller providing operations required by the Sidechain Generator.
    /// </summary>
    [Route("api/[controller]")]
    public class MainchainGeneratorServicesController : Controller
    {
        private IMainchainGeneratorServicesManager mainchainGeneratorServicesManager;

        public MainchainGeneratorServicesController(IMainchainGeneratorServicesManager mainchainGeneratorServicesManager)
        {
            this.mainchainGeneratorServicesManager = mainchainGeneratorServicesManager;
        }

        /// <summary>
        /// Initialized the sidechain by creating the multi-sig addresses and redeem scripts.
        /// Also actiivates the premine to mine coins into the sidechain multi-sig where they are
        /// locked and used to fund deposits into the sidechain.
        /// </summary>
        /// <param name="initializeSidechainRequest">Object containing the required parameters for this method.</param>
        /// <returns>A JSON response indicating either success (ok), or error details.</returns>
        [Route("init-sidechain")]
        [HttpPost]
        public async Task<IActionResult> InitializeSidechain([FromBody] InitSidechainRequest initializeSidechainRequest)
        {
            Guard.NotNull(initializeSidechainRequest, nameof(initializeSidechainRequest));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                await this.mainchainGeneratorServicesManager.InitSidechain(initializeSidechainRequest.SidechainName, initializeSidechainRequest.ApiPortForSidechain,
                    initializeSidechainRequest.MultiSigN, initializeSidechainRequest.MultiSigM, initializeSidechainRequest.FolderFedMemberKeys);
                return this.Ok();
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
