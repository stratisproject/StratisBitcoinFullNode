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
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
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

        private Network network;

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
        /// API implementation of RPC call.
        /// </summary>
        [Route("stop")]
        [HttpGet]
        public IActionResult Stop()
        {
            if (this.FullNode != null)
            {
                this.FullNode.Dispose();
                this.FullNode = null;
            }
            return this.Json(true);
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
                uint256 trxid;
                if (!uint256.TryParse(request.txid, out trxid))
                {
                    throw new ArgumentException(nameof(request.txid));
                }
                Transaction trx = this.pooledTransaction != null ? await this.pooledTransaction.GetTransaction(trxid) : null;
                if (trx == null)
                {
                    var blockStore = this.FullNode.NodeFeature<IBlockStore>();
                    trx = blockStore != null ? await blockStore.GetTrxAsync(trxid) : null;
                }
                if (trx == null)
                {
                    return this.Json(null);
                }
                if (request.verbose)
                {
                    ChainedHeader block = await this.GetTransactionBlockAsync(trxid);
                    return this.Json(new TransactionVerboseModel(trx, this.network, block, this.chainState?.ConsensusTip));
                }
                else
                {
                    return this.Json(new TransactionBriefModel(trx));
                }
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
                uint256 trxid;
                if (!uint256.TryParse(request.txid, out trxid))
                {
                    throw new ArgumentException(nameof(request.txid));
                }
                UnspentOutputs unspentOutputs = null;
                if (request.includeMemPool)
                {
                    unspentOutputs = this.pooledGetUnspentTransaction != null ? await this.pooledGetUnspentTransaction.GetUnspentTransactionAsync(trxid) : null;
                }
                else
                {
                    unspentOutputs = this.getUnspentTransaction != null ? await this.getUnspentTransaction.GetUnspentTransactionAsync(trxid) : null;
                }
                if (unspentOutputs == null)
                {
					return this.Json(null);
                }
                return this.Json(new GetTxOutModel(unspentOutputs, request.vout, this.network, this.chain.Tip));
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
                return this.Json(this.consensusLoop?.Tip.Height ?? -1);
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
                GetInfoModel model = new GetInfoModel
                {
                    Version = this.FullNode?.Version?.ToUint() ?? 0,
                    ProtocolVersion = (uint)(this.Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                    Blocks = this.chainState?.ConsensusTip?.Height ?? 0,
                    TimeOffset = this.connectionManager?.ConnectedPeers?.GetMedianTimeOffset() ?? 0,
                    Connections = this.connectionManager?.ConnectedPeers?.Count(),
                    Proxy = string.Empty,
                    Difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0,
                    Testnet = this.network.IsTest(),
                    RelayFee = this.Settings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC) ?? 0,
                    Errors = string.Empty,

                    //TODO: Wallet related infos: walletversion, balance, keypNetwoololdest, keypoolsize, unlocked_until, paytxfee
                    WalletVersion = null,
                    Balance = null,
                    KeypoolOldest = null,
                    KeypoolSize = null,
                    UnlockedUntil = null,
                    PayTxFee = null
                };

                return this.Json(model);
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
                Guard.NotNull(request.hash, nameof(request.hash));
                this.logger.LogDebug("API GetBlockHeader {0}", request.hash);

                if (!request.isJsonFormat)
                {
                    this.logger.LogError("Binary serialization is not supported for API '{0}'.", nameof(this.GetBlockHeader));
                    throw new NotImplementedException();
                }

                BlockHeaderModel model = null;
                if (this.chain != null)
                {
                    var blockHeader = this.chain.GetBlock(uint256.Parse(request.hash))?.Header;
                    if (blockHeader != null)
                    {
                        model = new BlockHeaderModel(blockHeader);
                    }
                }

                return this.Json(model);
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
                if (string.IsNullOrEmpty(request.address))
                {
                    throw new ArgumentNullException("address");
                }
                ValidatedAddress res = new ValidatedAddress();
                res.IsValid = false;
                
                // P2WPKH
                if (BitcoinWitPubKeyAddress.IsValid(request.address, ref this.network, out Exception _))
                {
                    res.IsValid = true;
                }
                // P2WSH
                else if (BitcoinWitScriptAddress.IsValid(request.address, ref this.network, out Exception _))
                {
                    res.IsValid = true;
                }
                // P2PKH
                else if (BitcoinPubKeyAddress.IsValid(request.address, ref this.network))
                {
                    res.IsValid = true;
                }
                // P2SH
                else if (BitcoinScriptAddress.IsValid(request.address, ref this.network))
                {
                    res.IsValid = true;
                }
                return this.Json(res);
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
                Guard.NotNull(this.connectionManager, nameof(this.connectionManager));
                IPEndPoint endpoint = NodeSettings.ConvertIpAddressToEndpoint(request.endpointStr, this.connectionManager.Network.DefaultPort);
                switch (request.command)
                {
                    case "add":
                        this.connectionManager.AddNodeAddress(endpoint);
                        break;

                    case "remove":
                        this.connectionManager.RemoveNodeAddress(endpoint);
                        break;

                    case "onetry":
                        this.connectionManager.ConnectAsync(endpoint).GetAwaiter().GetResult();
                        break;

                    default:
                        throw new ArgumentException("command");
                }
                return this.Json(true);
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
                // Connections.PeerNodeModel contained internal setters, so copied model into RPC.
                List<Models.PeerNodeModel> peerList = new List<Models.PeerNodeModel>();

                List<INetworkPeer> peers = this.connectionManager.ConnectedPeers.ToList();
                foreach (INetworkPeer peer in peers)
                {
                    if ((peer != null) && (peer.RemoteSocketAddress != null))
                    {
                        Models.PeerNodeModel peerNode = new Models.PeerNodeModel
                        {
                            Id = peers.IndexOf(peer),
                            Address = peer.RemoteSocketEndpoint.ToString()
                        };

                        if (peer.MyVersion != null)
                        {
                            peerNode.LocalAddress = peer.MyVersion.AddressReceiver?.ToString();
                            peerNode.Services = ((ulong)peer.MyVersion.Services).ToString("X");
                            peerNode.Version = (uint)peer.MyVersion.Version;
                            peerNode.SubVersion = peer.MyVersion.UserAgent;
                            peerNode.StartingHeight = peer.MyVersion.StartHeight;
                        }

                        ConnectionManagerBehavior connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                        if (connectionManagerBehavior != null)
                        {
                            peerNode.Inbound = connectionManagerBehavior.Inbound;
                            peerNode.IsWhiteListed = connectionManagerBehavior.Whitelisted;
                        }

                        if (peer.TimeOffset != null)
                        {
                            peerNode.TimeOffset = peer.TimeOffset.Value.Seconds;
                        }
                        peerList.Add(peerNode);
                    }
                }
                return this.Json(peerList);
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
                Guard.NotNull(this.chainState, nameof(this.chainState));
                return this.Json(this.chainState?.ConsensusTip?.HashBlock);
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
                Guard.NotNull(this.consensusLoop, nameof(this.consensusLoop));
                Guard.NotNull(this.chain, nameof(this.chain));
                this.logger.LogDebug("API GetBlockHash {0}", request.height);

                uint256 bestBlockHash = this.consensusLoop.Tip?.HashBlock;
                ChainedHeader bestBlock = bestBlockHash == null ? null : this.chain.GetBlock(bestBlockHash);
                if (bestBlock == null)
                {
                    return this.Json(null);
                }
                ChainedHeader block = this.chain.GetBlock(request.height);
                return block == null || block.Height > bestBlock.Height ? this.Json(null) : this.Json(block.HashBlock);
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
                MempoolManager mempoolManager = this.FullNode.NodeService<MempoolManager>();
                return this.Json(await mempoolManager.GetMempoolAsync());
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

        private async Task<ChainedHeader> GetTransactionBlockAsync(uint256 trxid)
        {
            ChainedHeader block = null;
            IBlockStore blockStore = this.FullNode.NodeFeature<IBlockStore>();

            uint256 blockid = blockStore != null ? await blockStore.GetTrxBlockIdAsync(trxid) : null;
            if (blockid != null)
                block = this.chain?.GetBlock(blockid);

            return block;
        }

        private Target GetNetworkDifficulty()
        {
            return this.networkDifficulty?.GetNetworkDifficulty();
        }
    }
}
