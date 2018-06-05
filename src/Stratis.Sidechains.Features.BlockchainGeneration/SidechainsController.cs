using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    [Route("api/[controller]")]
    public class SidechainsController : Controller
    {
        private ISidechainsManager sidechainsManager;

        public SidechainsController(ISidechainsManager sidechainsManager)
        {
            this.sidechainsManager = sidechainsManager;
        }

        [Route("list-sidechains")]
        [HttpGet]
        public async Task<JsonResult> ListSidechains()
        {
            try
            {
                var sidechains = await this.sidechainsManager.ListSidechains().ConfigureAwait(false);
                return this.Json(sidechains);
            }
            catch (Exception e)
            {
                return this.Json($"Could not get sidechain info. {e.Message}.");
            }
        }

        [Route("new-sidechain")]
        [HttpPost]
        public async Task<IActionResult> NewSidechain([FromBody] SidechainInfoRequest sidechainInfo)
        {
            try
            {
                await this.sidechainsManager.NewSidechain(sidechainInfo);
                return this.Ok();
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could add sidechain info.", e.ToString());
            }
        }

        [Route("get-coindetails")]
        [HttpGet]
        public async Task<IActionResult> GetCoinDetails()
        {
            try
            {
                var coinDetails = await sidechainsManager.GetCoinDetails();
                return Json(coinDetails);
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Failed to retrieve coin details for the current node", e.ToString());
            }
        }
    }
}