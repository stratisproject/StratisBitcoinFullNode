using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>
    /// Controller providing operations on a blockstore.
    /// </summary>
    [Route("api/[controller]")]
    public class BlockStoreController : Controller
    {
        private readonly IBlockStoreCache blockStoreCache;
        private readonly IBlockRepository blockRepository;
        private readonly ILogger logger;
        private readonly IFullNode fullNode;
        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly IChainState chainState;

        public BlockStoreController(
            ILoggerFactory loggerFactory,
            IBlockStoreCache blockStoreCache,
            IBlockRepository blockRepository,
            Network network,
            ConcurrentChain chain,
            IChainState chainState)
        {
            Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
            Guard.NotNull(blockRepository, nameof(blockRepository));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(network, nameof(network));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.blockRepository = blockRepository;
            this.blockStoreCache = blockStoreCache;
            this.network = network;
            this.chain = chain;
            this.chainState = chainState;
        }

        [Route("block")]
        [HttpGet]
        public async Task<IActionResult> GetBlockAsync([FromQuery] SearchByHashRequest query)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            this.logger.LogTrace("({0}:'{1}')", nameof(SearchByHashRequest.Hash), query.Hash);

            try
            {
                Block block = await this.blockStoreCache.GetBlockAsync(uint256.Parse(query.Hash)).ConfigureAwait(false);
                if (block == null) return new NotFoundObjectResult("Block not found");
                return query.OutputJson
                    ? this.Json(new BlockModel(block))
                    : this.Json(block);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }


        [Route("getrawtransaction")]
        [HttpGet]
        public async Task<IActionResult> GetRawTransactionAsync([FromQuery] GetRawTransactionRequest query)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            this.logger.LogTrace("({0}:'{1}')", nameof(GetRawTransactionRequest.txid), query.txid);

            try
            {
                uint256 trxid = uint256.Parse(query.txid);
                Transaction trx = await this.blockRepository?.GetTrxAsync(trxid);
                if (trx == null)
                {
                    return new NotFoundObjectResult("Transaction not found");
                }

                if (query.verbose != 0 && query.OutputJson != false)
                {
                    uint256 blockid = await this.blockRepository?.GetTrxBlockIdAsync(trxid);
                    ChainedHeader block = this.chain?.GetBlock(blockid);
                    return this.Json(new TransactionVerboseModel(trx, this.network, block, this.chainState?.ConsensusTip));
                }
                else if (query.verbose == 0 && query.OutputJson != false)
                {
                    return this.Json(new TransactionBriefModel(trx));
                }
                else
                {
                    return this.Json(trx);
                }
            }
            catch (FormatException e)
            {
                return new NotFoundObjectResult("Invalid Hex String");
            }
            catch (NullReferenceException e)
            {
                return new NotFoundObjectResult("Invalid Hex String");
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