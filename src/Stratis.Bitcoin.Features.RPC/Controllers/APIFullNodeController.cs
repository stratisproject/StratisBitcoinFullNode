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
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings)
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
        }

        /// <summary>
        /// Stops the full node.
        /// </summary>
        /// <returns>No content</returns>
        [Route("stop")]
        [HttpGet]
        public async Task<IActionResult> Stop()
        {
            await SharedRemoteMethods.Stop(this.FullNode);
            return NoContent();
        }

        /// <summary>
        /// Gets a raw, possibly pooled, transaction from the full node. 
        /// API implementation of RPC call. 
        /// </summary>
        /// <param name="transactionRequestModel">A <see cref="TransactionRequestModel"/> formated request containing a txid and verbose indicator.</param>
        /// <returns>JSON formatted <see cref="TransactionBriefModel"/> or <see cref="TransactionVerboseModel"/>. Returns null for invalid TxID</returns>
        [Route("getrawtransaction")]
        [HttpGet]
        public async Task<IActionResult> GetRawTransactionAsync(GetRawTransactionRequestModel request)
        {
            try
            {
                return this.Json(await SharedRemoteMethods.GetRawTransactionAsync(request.txid, request.verbose, this.pooledTransaction,
                this.FullNode, this.network, this.chainState, this.chain));
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
        /// <returns>JSON formatted <see cref="GetTxOutModel"/>. Returns null if no unspentoutputs.</returns>
        [Route("gettxout")]
        [HttpGet]
        public async Task<IActionResult> GetTxOutAsync(GetTxOutRequestModel request)
        {
            try
            {
                return this.Json(await SharedRemoteMethods.GetTxOutAsync(request.txid, request.vout, request.includeMemPool,
                    this.pooledGetUnspentTransaction, this.getUnspentTransaction, this.network, this.chain));
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
        /// <returns>JSON formatted int with the consensus tip height</returns>
        [Route("getblockcount")]
        [HttpGet]
        public IActionResult GetBlockCount()
        {
            try
            {
                return this.Json(SharedRemoteMethods.GetBlockCount(this.consensusLoop));
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
        /// <returns>JSON formatted <see cref="GetInfoModel"/></returns>
        [Route("getinfo")]
        [HttpGet]
        public IActionResult GetInfo()
        {
            try
            {
                return this.Json(SharedRemoteMethods.GetInfo(this.network, this.FullNode, 
                    this.Settings, this.chainState, this.connectionManager,
                    this.networkDifficulty));
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
        /// <param name="request">A <see cref="GetBlockHeaderRequestModel"/> formatted request containing a block hash</param>
        /// <returns>JSON formatted <see cref="BlockHeaderModel"/></returns>
        [Route("getblockheader")]
        [HttpGet]
        public IActionResult GetBlockHeader(GetBlockHeaderRequestModel request)
        {
            try
            {
                return this.Json(SharedRemoteMethods.GetBlockHeader(request.hash, request.isJsonFormat, this.logger, this.chain));
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
        /// <returns>A <see cref="ValidatedAddress"/> containing a boolean indicating address validity</returns>
        [Route("validateaddress")]
        [HttpGet]
        public IActionResult ValidateAddress(ValidateAddressRequestModel request)
        {
            try
            {
                return this.Json(SharedRemoteMethods.ValidateAddress(request.address, this.network));
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
        /// <param name="request">A <see cref="AddNodeRequestModel"/> formatted request containing an endpoint and command. </param>
        /// <returns>JSON formatted bool indication success/failure.</returns>
        [Route("addnode")]
        [HttpGet]
        public IActionResult AddNode(AddNodeRequestModel request)
        {
            try
            {
                return this.Json(SharedRemoteMethods.AddNode(request.str_endpoint, request.command, this.connectionManager));
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
        /// <returns>JSON formatted <see cref="Models.PeerNodeModel"/> list of connected nodes.</returns>
        [Route("getpeerinfo")]
        [HttpGet]
        public IActionResult GetPeerInfo()
        {
            try
            {
                return this.Json(SharedRemoteMethods.GetPeerInfo(this.connectionManager));
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
        /// <returns>JSON formatted <see cref="uint256"/> of best block hash</returns>
        [Route("getbestblockhash")]
        [HttpGet]
        public IActionResult GetBestBlockHash()
        {
            try
            {
                return this.Json(SharedRemoteMethods.GetBestBlockHash(this.chainState));
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
        /// <param name="request">A <see cref="GetBlockHashRequestModel"/> request containing the height</param>
        /// <returns>A JSON formatted hash of the block at the given height</returns>
        [Route("getblockhash")]
        [HttpGet]
        public IActionResult GetBlockHash(GetBlockHashRequestModel request)
        {
            try
            {
                return this.Json(SharedRemoteMethods.GetBlockHash(request.height, this.consensusLoop, this.chain, this.logger));
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
        /// <returns>A JSON formatted list containing the memory pool contents.</returns>
        [Route("getrawmempool")]
        [HttpGet]
        public async Task<IActionResult> GetRawMempoolAsync()
        {
            try
            {
                return this.Json(await SharedRemoteMethods.GetRawMempoolAsync(this.FullNode));
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
