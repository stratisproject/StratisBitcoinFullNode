using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.SidechainGeneratorServices.Models;

namespace Stratis.FederatedPeg.Features.SidechainGeneratorServices
{
    /// <summary>
    /// Controller providing operations required by the Sidechain Generator.
    /// </summary>
    [Route("api/[controller]")]
    public class SidechainGeneratorServicesController : Controller
    {
        private ISidechainGeneratorServicesManager sidechainGeneratorServicesManager;

        public SidechainGeneratorServicesController(ISidechainGeneratorServicesManager sidechainGeneratorServicesManager)
        {
            this.sidechainGeneratorServicesManager = sidechainGeneratorServicesManager;
        }

        /// <summary>
        /// Gets the name of the sidechain that the node is running.
        /// </summary>
        /// <returns>The name of the sidechain.</returns>
        [Route("get-sidechainname")]
        [HttpGet]
        public IActionResult GetSidechainName()
        {
            try
            {
                var sidechainName = this.sidechainGeneratorServicesManager.GetSidechainName();
                return this.Json(sidechainName);
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Outputs the sidechain multi-sig redeem script needed for transaction signing and 
        /// the address of the multi-sig.
        /// </summary>
        /// <param name="outputScriptPubKeyAndAddressRequest">Object containing the required parameters for this method.</param>
        /// <returns>A JSON response indicating either success (ok), or error details.</returns>
        [Route("output-scriptpubkeyandaddress")]
        [HttpPost]
        public IActionResult OutputScriptPubKeyAndAddress([FromBody] OutputScriptPubKeyAndAddressRequest outputScriptPubKeyAndAddressRequest)
        {
            Guard.NotNull(outputScriptPubKeyAndAddressRequest, nameof(outputScriptPubKeyAndAddressRequest));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.sidechainGeneratorServicesManager.OutputScriptPubKeyAndAddress(
                    outputScriptPubKeyAndAddressRequest.MultiSigM,
                    outputScriptPubKeyAndAddressRequest.MultiSigN,
                    outputScriptPubKeyAndAddressRequest.FederationFolder);
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
