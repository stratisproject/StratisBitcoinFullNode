using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class BlockStoreController : Controller
    {
        private readonly IBlockStoreCache blockStoreCache;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public BlockStoreController(
            ILoggerFactory loggerFactory,
            IBlockStoreCache blockStoreCache,
            IConnectionManager connectionManager,
            Network network,
            ConcurrentChain chain,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.blockStoreCache = blockStoreCache;
            this.connectionManager = connectionManager;
            this.network = network;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
        }

        [Route("getblock")]
        [HttpGet]
        public async Task<IActionResult> GetBlockAsync([FromQuery] ObjectByHashQueryModel query)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            this.logger.LogTrace("({0}:'{1}')", nameof(ObjectByHashQueryModel.Hash), query.Hash);

            try
            {
                var block = await this.blockStoreCache.GetBlockAsync(uint256.Parse(query.Hash)).ConfigureAwait(false);
                if(block == null) return new NotFoundObjectResult("Block not found");
                return query.OutputJson 
                    ? this.Json(JObject.Parse(block.ToJson()))
                    : this.Json(block);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
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