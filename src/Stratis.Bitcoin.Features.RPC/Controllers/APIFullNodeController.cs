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
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    [Route("api/[controller]")]
    public class APIFullNodeController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IPooledTransaction pooledTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        private readonly INetworkDifficulty networkDifficulty;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        private readonly IConsensusLoop consensusLoop;

        private readonly Network network;

        private readonly ConcurrentChain chain;

        private readonly IChainState chainState;

        private readonly IConnectionManager connectionManager;

        private readonly IFullNode fullNode;

        private readonly NodeSettings nodeSettings;

        public APIFullNodeController(
            ILoggerFactory loggerFactory,
            Network network,
            ConcurrentChain chain,
            IChainState chainState,
            IConnectionManager connectionManager,
            IPooledTransaction pooledTransaction = null,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
            IGetUnspentTransaction getUnspentTransaction = null,
            INetworkDifficulty networkDifficulty = null,
            IConsensusLoop consensusLoop = null,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.pooledTransaction = pooledTransaction;
            this.pooledGetUnspentTransaction = pooledGetUnspentTransaction;
            this.getUnspentTransaction = getUnspentTransaction;
            this.networkDifficulty = networkDifficulty;
            this.consensusLoop = consensusLoop;
            this.network = network;
            this.chain = chain;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.fullNode = fullNode;
            this.nodeSettings = nodeSettings;
        }

        /// <summary>
        /// Stops the full node.
        /// </summary>
        /// <returns>A <see cref="NoContentResult"/></returns>
        [Route("stop")]
        [HttpGet]
        public async Task<IActionResult> StopAsync()
        {
            await SharedRemoteMethods.Stop(this.fullNode).ConfigureAwait(false);
            return this.NoContent();
        }

        /// <summary>
        /// Gets a raw, possibly pooled, transaction from the full node. 
        /// API implementation of RPC call. 
        /// </summary>
        /// <param name="request">A <see cref="GetRawTransactionRequestModel"/> formated request containing a txid and verbose indicator.</param>
        /// <returns>Json formatted <see cref="TransactionBriefModel"/> or <see cref="TransactionVerboseModel"/>. Returns <see cref="IActionResult"/> formatted error if otherwise fails.</returns>
        [Route("getrawtransaction")]
        [HttpGet]
        public async Task<IActionResult> GetRawTransactionAsync(GetRawTransactionRequestModel request)
        {
            try
            {
                var result = await SharedRemoteMethods.GetRawTransactionAsync(request.txid, request.verbose, this.pooledTransaction,
                    this.fullNode, this.network, this.chainState, this.chain).ConfigureAwait(false);
                return this.Json(result);
            } 
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the unspent outputs of a transaction id and vout number.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="GetTxOutRequestModel"/> formatted request containing txid, vout, and if should check memPool.</param>
        /// <returns>Json formatted <see cref="GetTxOutModel"/>. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("gettxout")]
        [HttpGet]
        public async Task<IActionResult> GetTxOutAsync(GetTxOutRequestModel request)
        {
            try
            {
                var result = await SharedRemoteMethods.GetTxOutAsync(request.txid, request.vout, request.includeMemPool,
                    this.pooledGetUnspentTransaction, this.getUnspentTransaction, this.network, this.chain).ConfigureAwait(false);
                return this.Json(result);
            } 
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
        /// <summary>
        /// Gets the current consensus tip height.
        /// API implementation of RPC call.
        /// </summary>
        /// <returns>Json formatted int with the consensus tip height. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getblockcount")]
        [HttpGet]
        public IActionResult GetBlockCount()
        {
            try
            {
                var result = SharedRemoteMethods.GetBlockCount(this.consensusLoop);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets general information about the full node.
        /// API implementation of RPC call.
        /// </summary>
        /// <returns>Json formatted <see cref="GetInfoModel"/>. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getinfo")]
        [HttpGet]
        public IActionResult GetInfo()
        {
            try
            {
                var result = SharedRemoteMethods.GetInfo(this.network, this.fullNode,
                    this.nodeSettings, this.chainState, this.connectionManager,
                    this.networkDifficulty);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the block header of the block identified by the hash.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="GetBlockHeaderRequestModel"/> formatted request containing a block hash.</param>
        /// <returns>Json formatted <see cref="BlockHeaderModel"/>. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getblockheader")]
        [HttpGet]
        public IActionResult GetBlockHeader(GetBlockHeaderRequestModel request)
        {
            try
            {
                var result = SharedRemoteMethods.GetBlockHeader(request.hash, request.isJsonFormat, this.logger, this.chain);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns information about a bech32 or base58 bitcoin address.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="ValidateAddressRequestModel"/> containing a bech32 or base58 BitcoinAddress to validate.</param>
        /// <returns>Json formatted <see cref="ValidatedAddress"/> containing a boolean indicating address validity. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("validateaddress")]
        [HttpGet]
        public IActionResult ValidateAddress(ValidateAddressRequestModel request)
        {
            try
            {
                var result = SharedRemoteMethods.ValidateAddress(request.address, this.network);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Adds a node to the connection manager.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="AddNodeRequestModel"/> formatted request containing an endpoint and command.</param>
        /// <returns>Json formatted <c>True</c> indicating success. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("addnode")]
        [HttpGet]
        public IActionResult AddNode(AddNodeRequestModel request)
        {
            try
            {
                var result = SharedRemoteMethods.AddNode(request.Endpoint, request.command, this.connectionManager);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets peer information from the connection manager.
        /// API implementation of RPC call.
        /// </summary>
        /// <see cref="https://github.com/bitcoin/bitcoin/blob/0.14/src/rpc/net.cpp"/>
        /// <returns>Json formatted <see cref="List{T}<see cref="Models.PeerNodeModel"/>"/> of connected nodes. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getpeerinfo")]
        [HttpGet]
        public IActionResult GetPeerInfo()
        {
            try
            {
                var result = SharedRemoteMethods.GetPeerInfo(this.connectionManager);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Get the hash of the block at the consensus tip.
        /// API implementation of RPC call.
        /// </summary>
        /// <returns>Json formatted <see cref="uint256"/> of best block hash. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getbestblockhash")]
        [HttpGet]
        public IActionResult GetBestBlockHash()
        {
            try
            {
                var result = SharedRemoteMethods.GetBestBlockHash(this.chainState);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the hash of the block at the given height.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="GetBlockHashRequestModel"/> request containing the height.</param>
        /// <returns>Json formatted <see cref="uint256"/> hash of the block at the given height. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getblockhash")]
        [HttpGet]
        public IActionResult GetBlockHash(GetBlockHashRequestModel request)
        {
            try
            {
                var result = SharedRemoteMethods.GetBlockHash(request.height, this.consensusLoop, this.chain, this.logger);
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Lists the contents of the memory pool.
        /// </summary>
        /// <returns>Json formatted <see cref="List{T}<see cref="uint256"/>"/> containing the memory pool contents. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getrawmempool")]
        [HttpGet]
        public async Task<IActionResult> GetRawMempoolAsync()
        {
            try
            {
                var result = await SharedRemoteMethods.GetRawMempoolAsync(this.FullNode).ConfigureAwait(false);
                return this.Json(result);
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
        /// <returns>An <see cref="IActionResult"/> containing the errors with messages.</returns>
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
