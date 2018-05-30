using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Controllers
{
    [Route("api/[controller]")]
    public class NodeController : Controller
    {
        /// <summary>Full Node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Provider of date and time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The settings for the node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>The connection manager.</summary>
        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        private readonly INetworkDifficulty networkDifficulty;

        private readonly IPooledTransaction pooledTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        // Not readonly because of ValidateAddress
        private Network network;

        public NodeController(IFullNode fullNode, ILoggerFactory loggerFactory, 
            IDateTimeProvider dateTimeProvider, IChainState chainState, 
            NodeSettings nodeSettings, IConnectionManager connectionManager,
            ConcurrentChain chain, Network network, IPooledTransaction pooledTransaction = null,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
            IGetUnspentTransaction getUnspentTransaction = null,
            INetworkDifficulty networkDifficulty = null)
        {
            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.chainState = chainState;
            this.nodeSettings = nodeSettings;
            this.connectionManager = connectionManager;
            this.chain = chain;
            this.network = network;
            this.pooledTransaction = pooledTransaction;
            this.pooledGetUnspentTransaction = pooledGetUnspentTransaction;
            this.getUnspentTransaction = getUnspentTransaction;
            this.networkDifficulty = networkDifficulty;
        }

        /// <summary>
        /// Returns some general information about the status of the underlying node.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("status")]
        public IActionResult Status()
        {
            StatusModel model = new StatusModel
            {
                Version = this.fullNode.Version?.ToString() ?? "0",
                ProtocolVersion = (uint)(this.nodeSettings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                Difficulty = GetNetworkDifficulty(this.networkDifficulty)?.Difficulty ?? 0,
                Agent = this.nodeSettings.Agent,
                ProcessId = Process.GetCurrentProcess().Id,
                Network = this.fullNode.Network.Name,
                ConsensusHeight = this.chainState.ConsensusTip.Height,
                DataDirectoryPath = this.nodeSettings.DataDir,
                Testnet = this.network.IsTest(),
                RelayFee = this.nodeSettings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC) ?? 0,
                RunningTime = this.dateTimeProvider.GetUtcNow() - this.fullNode.StartTime
            };

            // Add the list of features that are enabled.
            foreach (IFullNodeFeature feature in this.fullNode.Services.Features)
            {
                model.EnabledFeatures.Add(feature.GetType().ToString());

                // Include BlockStore Height if enabled
                if (feature is IBlockStore)
                    model.BlockStoreHeight = ((IBlockStore)feature).GetHighestPersistedBlock().Height;
            }

            // Add the details of connected nodes.
            foreach (INetworkPeer peer in this.connectionManager.ConnectedPeers)
            {
                ConnectionManagerBehavior connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                ChainHeadersBehavior chainHeadersBehavior = peer.Behavior<ChainHeadersBehavior>();

                ConnectedPeerModel connectedPeer = new ConnectedPeerModel
                {
                    Version = peer.PeerVersion != null ? peer.PeerVersion.UserAgent : "[Unknown]",
                    RemoteSocketEndpoint = peer.RemoteSocketEndpoint.ToString(),
                    TipHeight = chainHeadersBehavior.PendingTip != null ? chainHeadersBehavior.PendingTip.Height : peer.PeerVersion?.StartHeight ?? -1,
                    IsInbound = connectionManagerBehavior.Inbound
                };

                if (connectedPeer.IsInbound)
                {
                    model.InboundPeers.Add(connectedPeer);
                }
                else
                {
                    model.OutboundPeers.Add(connectedPeer);
                }
            }

            return this.Json(model);
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
                Guard.NotNull(this.logger, nameof(this.logger));
                if (string.IsNullOrEmpty(request.hash))
                {
                    throw new ArgumentNullException("hash");
                }

                this.logger.LogDebug("GetBlockHeader {0}", request.hash);
                if (!request.isJsonFormat)
                {
                    this.logger.LogError("Binary serialization is not supported'{0}'.", nameof(GetBlockHeader));
                    throw new NotImplementedException();
                }

                BlockHeaderModel model = null;
                if (this.chain != null)
                {
                    BlockHeader blockHeader = this.chain.GetBlock(uint256.Parse(request.hash))?.Header;
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
                Guard.NotNull(this.fullNode, nameof(this.fullNode));
                Guard.NotNull(this.network, nameof(this.network));
                Guard.NotNull(this.chain, nameof(this.chain));

                uint256 txid;
                if (!uint256.TryParse(request.txid, out txid))
                {
                    throw new ArgumentException(nameof(request.txid));
                }

                // First tries to find a pooledTransaction. If can't, will grab it from the blockstore if it exists. 
                Transaction trx = this.pooledTransaction != null ? await this.pooledTransaction.GetTransaction(txid) : null;
                if (trx == null)
                {
                    IBlockStore blockStore = this.fullNode.NodeFeature<IBlockStore>();
                    trx = blockStore != null ? await blockStore.GetTrxAsync(txid) : null;
                }

                if (trx == null)
                {
                    return this.Json(null);
                }

                if (request.verbose)
                {
                    ChainedHeader block = await GetTransactionBlockAsync(txid, this.fullNode, this.chain);
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
        /// Returns information about a bech32 or base58 bitcoin address.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="ValidateAddressRequestModel"/> containing a bech32 or base58 BitcoinAddress to validate.</param>
        /// <returns>Json formatted <see cref="ValidatedAddress"/> containing a boolean indicating address validity. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if address provided is null or empty.</exception>
        [Route("validateaddress")]
        [HttpGet]
        public IActionResult ValidateAddress(ValidateAddressRequestModel request)
        {
            try
            {
                Guard.NotNull(this.network, nameof(this.network));
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
                Guard.NotNull(this.network, nameof(this.network));
                Guard.NotNull(this.chain, nameof(this.chain));
                uint256 trxid;

                if (!uint256.TryParse(request.txid, out trxid))
                {
                    throw new ArgumentException(nameof(request.txid));
                }

                uint vout;
                if (!uint.TryParse(request.vout, out vout))
                {
                    throw new ArgumentException(nameof(request.vout));
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

                return this.Json(new GetTxOutModel(unspentOutputs, vout, this.network, this.chain.Tip));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Trigger a shoutdown of the current running node.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("shutdown")]
        [Route("stop")]
        public IActionResult Shutdown()
        {
            if (this.fullNode != null)
            {
                // start the node shutdown process
                this.fullNode.Dispose();
            }
            return this.Ok();
        }

        /// <summary>
        /// Retrieves a transaction block given a valid hash.
        /// This function is used by other methods in this class and not explicitly by RPC/API.
        /// </summary>
        /// <param name="trxid">A valid uint256 hash</param>
        /// <param name="fullNode">The full node. Used to access blockstore.</param>
        /// <param name="chain">The full node's chain. Used to get block.</param>
        /// <returns>A <see cref="ChainedHeader"/> for the given transaction hash. Returns <c>null</c> if fails.</returns>
        internal static async Task<ChainedHeader> GetTransactionBlockAsync(uint256 trxid,
            IFullNode fullNode, ChainBase chain)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            ChainedHeader block = null;
            IBlockStore blockStore = fullNode.NodeFeature<IBlockStore>();
            uint256 blockid = blockStore != null ? await blockStore.GetTrxBlockIdAsync(trxid) : null;
            if (blockid != null)
            {
                block = chain?.GetBlock(blockid);
            }

            return block;
        }

        /// <summary>
        /// Gets the current network difficulty. 
        /// </summary>
        /// <param name="networkDifficulty">The full node's network difficulty.</param>
        /// <returns>A network difficulty <see cref="Target"/>. Returns <c>null</c> if fails.</returns>
        internal static Target GetNetworkDifficulty(INetworkDifficulty networkDifficulty = null)
        {
            return networkDifficulty?.GetNetworkDifficulty();
        }
    }
}
