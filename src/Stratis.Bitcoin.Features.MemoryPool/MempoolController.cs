using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Controller providing operations on the Mempool.
    /// </summary>
    public class MempoolController : FeatureController
    {
        public MempoolManager MempoolManager { get; private set; }
        private readonly ILogger logger;

        public MempoolController(ILoggerFactory loggerFactory, MempoolManager mempoolManager)
        {
            Guard.NotNull(mempoolManager, nameof(mempoolManager));

            this.MempoolManager = mempoolManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("getrawmempool")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionDescription("Lists the contents of the memory pool.")]
        public Task<List<uint256>> GetRawMempool()
        {
            return this.MempoolManager.GetMempoolAsync();
        }

        /// <summary>
        /// Lists the contents of the memory pool.
        /// </summary>
        /// <returns>Json formatted <see cref="List{T}<see cref="uint256"/>"/> containing the memory pool contents. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("api/[controller]/getrawmempool")]
        [HttpGet]
        public async Task<IActionResult> GetRawMempoolAsync()
        {
            try
            {
                return this.Json(await this.GetRawMempool().ConfigureAwait(false));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
