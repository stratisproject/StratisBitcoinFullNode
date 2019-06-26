﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = NLog.LogLevel;
using Target = NBitcoin.Target;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Provides methods that interact with the full node.
    /// </summary>
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

        /// <summary>Thread safe access to the best chain of block headers from genesis.</summary>
        private readonly ChainIndexer chainIndexer;

        /// <summary>An interface implementation used to retrieve the network's difficulty target.</summary>
        private readonly INetworkDifficulty networkDifficulty;

        /// <summary>An interface implementaiton used to retrieve a pooled transaction.</summary>
        private readonly IPooledTransaction pooledTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        /// <summary>Specification of the network the node runs on.</summary>
        private Network network; // Not readonly because of ValidateAddress

        /// <summary>An interface implementation for the blockstore.</summary>
        private readonly IBlockStore blockStore;

        /// <summary>Provider for creating and managing background async loop tasks.</summary>
        private readonly IAsyncProvider asyncProvider;

        public NodeController(
            ChainIndexer chainIndexer,
            IChainState chainState,
            IConnectionManager connectionManager,
            IDateTimeProvider dateTimeProvider,
            IFullNode fullNode,
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            Network network,
            IAsyncProvider asyncProvider,
            IBlockStore blockStore = null,
            IGetUnspentTransaction getUnspentTransaction = null,
            INetworkDifficulty networkDifficulty = null,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
            IPooledTransaction pooledTransaction = null)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chainIndexer, nameof(chainIndexer));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(connectionManager, nameof(connectionManager));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));

            this.chainIndexer = chainIndexer;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.dateTimeProvider = dateTimeProvider;
            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.nodeSettings = nodeSettings;
            this.asyncProvider = asyncProvider;

            this.blockStore = blockStore;
            this.getUnspentTransaction = getUnspentTransaction;
            this.networkDifficulty = networkDifficulty;
            this.pooledGetUnspentTransaction = pooledGetUnspentTransaction;
            this.pooledTransaction = pooledTransaction;
        }

        /// <summary>
        /// Gets general information about this full node including the version,
        /// protocol version, network name, coin ticker, and consensus height.
        /// </summary>
        /// <returns>A <see cref="StatusModel"/> with information about the node.</returns>
        [HttpGet]
        [Route("status")]
        public IActionResult Status()
        {
            // Output has been merged with RPC's GetInfo() since they provided similar functionality.
            var model = new StatusModel
            {
                Version = this.fullNode.Version?.ToString() ?? "0",
                ProtocolVersion = (uint)(this.nodeSettings.ProtocolVersion),
                Difficulty = GetNetworkDifficulty(this.networkDifficulty)?.Difficulty ?? 0,
                Agent = this.connectionManager.ConnectionSettings.Agent,
                ProcessId = Process.GetCurrentProcess().Id,
                Network = this.fullNode.Network.Name,
                ConsensusHeight = this.chainState.ConsensusTip?.Height,
                DataDirectoryPath = this.nodeSettings.DataDir,
                Testnet = this.network.IsTest(),
                RelayFee = this.nodeSettings.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC) ?? 0,
                RunningTime = this.dateTimeProvider.GetUtcNow() - this.fullNode.StartTime,
                CoinTicker = this.network.CoinTicker,
                State = this.fullNode.State.ToString()
            };

            // Add the list of features that are enabled.
            foreach (IFullNodeFeature feature in this.fullNode.Services.Features)
            {
                model.FeaturesData.Add(new FeatureData
                {
                    Namespace = feature.GetType().ToString(),
                    State = feature.State
                });
            }

            // Include BlockStore Height if enabled
            if (this.chainState.BlockStoreTip != null)
                model.BlockStoreHeight = this.chainState.BlockStoreTip.Height;

            // Add the details of connected nodes.
            foreach (INetworkPeer peer in this.connectionManager.ConnectedPeers)
            {
                var connectionManagerBehavior = peer.Behavior<IConnectionManagerBehavior>();
                var chainHeadersBehavior = peer.Behavior<ConsensusManagerBehavior>();

                var connectedPeer = new ConnectedPeerModel
                {
                    Version = peer.PeerVersion != null ? peer.PeerVersion.UserAgent : "[Unknown]",
                    RemoteSocketEndpoint = peer.RemoteSocketEndpoint.ToString(),
                    TipHeight = chainHeadersBehavior.BestReceivedTip != null ? chainHeadersBehavior.BestReceivedTip.Height : peer.PeerVersion?.StartHeight ?? -1,
                    IsInbound = peer.Inbound
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
        /// Gets the block header of a block identified by a block hash.
        /// </summary>
        /// <param name="hash">The hash of the block to retrieve.</param>
        /// <param name="isJsonFormat">A flag that specifies whether to return the block header in the JSON format. Defaults to true. A value of false is currently not supported.</param>
        /// <returns>Json formatted <see cref="BlockHeaderModel"/>. <c>null</c> if block not found. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        /// <exception cref="NotImplementedException">Thrown if isJsonFormat = false</exception>"
        /// <exception cref="ArgumentException">Thrown if hash is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if logger is not provided.</exception>
        /// <remarks>Binary serialization is not supported with this method.</remarks>
        [Route("getblockheader")]
        [HttpGet]
        public IActionResult GetBlockHeader([FromQuery] string hash, bool isJsonFormat = true)
        {
            try
            {
                Guard.NotEmpty(hash, nameof(hash));

                this.logger.LogDebug("GetBlockHeader {0}", hash);
                if (!isJsonFormat)
                {
                    this.logger.LogError("Binary serialization is not supported.");
                    throw new NotImplementedException();
                }

                BlockHeaderModel model = null;
                BlockHeader blockHeader = this.chainIndexer?.GetHeader(uint256.Parse(hash))?.Header;
                if (blockHeader != null)
                {
                    model = new BlockHeaderModel(blockHeader);
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
        /// Gets a raw transaction that is present on this full node.
        /// This method first searches the transaction pool and then tries the block store.
        /// </summary>
        /// <param name="trxid">The transaction ID (a hash of the trancaction).</param>
        /// <param name="verbose">A flag that specifies whether to return verbose information about the transaction.</param>
        /// <returns>Json formatted <see cref="TransactionBriefModel"/> or <see cref="TransactionVerboseModel"/>. <c>null</c> if transaction not found. Returns <see cref="IActionResult"/> formatted error if otherwise fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if fullNode, network, or chain are not available.</exception>
        /// <exception cref="ArgumentException">Thrown if trxid is empty or not a valid<see cref="uint256"/>.</exception>
        /// <remarks>Requires txindex=1, otherwise only txes that spend or create UTXOs for a wallet can be returned.</remarks>
        [Route("getrawtransaction")]
        [HttpGet]
        public async Task<IActionResult> GetRawTransactionAsync([FromQuery] string trxid, bool verbose = false)
        {
            try
            {
                Guard.NotEmpty(trxid, nameof(trxid));

                uint256 txid;
                if (!uint256.TryParse(trxid, out txid))
                {
                    throw new ArgumentException(nameof(trxid));
                }

                // First tries to find a pooledTransaction. If can't, will retrieve it from the blockstore if it exists.
                Transaction trx = this.pooledTransaction != null ? await this.pooledTransaction.GetTransaction(txid).ConfigureAwait(false) : null;
                if (trx == null)
                {
                    trx = this.blockStore?.GetTransactionById(txid);
                }

                if (trx == null)
                {
                    return this.Json(null);
                }

                if (verbose)
                {
                    ChainedHeader block = this.GetTransactionBlock(txid, this.fullNode, this.chainIndexer);
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
        /// Gets a JSON representation for a given transaction in hex format.
        /// </summary>
        /// <param name="request">A class containing the necessary parameters for a block search request.</param>
        /// <returns>The JSON representation of the transaction.</returns>
        [HttpPost]
        [Route("decoderawtransaction")]
        public IActionResult DecodeRawTransaction([FromBody] DecodeRawTransactionModel request)
        {
            try
            {
                if (!this.ModelState.IsValid)
                {
                    return ModelStateErrors.BuildErrorResponse(this.ModelState);
                }

                return this.Json(new TransactionVerboseModel(this.network.CreateTransaction(request.RawHex), this.network));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Validates a bech32 or base58 bitcoin address.
        /// </summary>
        /// <param name="address">A Bitcoin address to validate in a string format.</param>
        /// <returns>Json formatted <see cref="ValidatedAddress"/> containing a boolean indicating address validity. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        /// <exception cref="ArgumentException">Thrown if address provided is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if network is not provided.</exception>
        [Route("validateaddress")]
        [HttpGet]
        public IActionResult ValidateAddress([FromQuery] string address)
        {
            try
            {
                Guard.NotEmpty(address, nameof(address));

                var res = new ValidatedAddress
                {
                    IsValid = false
                };
                // P2WPKH
                if (BitcoinWitPubKeyAddress.IsValid(address, this.network, out Exception _))
                {
                    res.IsValid = true;
                }

                // P2WSH
                else if (BitcoinWitScriptAddress.IsValid(address, this.network, out Exception _))
                {
                    res.IsValid = true;
                }

                // P2PKH
                else if (BitcoinPubKeyAddress.IsValid(address, this.network))
                {
                    res.IsValid = true;
                }

                // P2SH
                else if (BitcoinScriptAddress.IsValid(address, this.network))
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
        /// Gets the unspent outputs of a specific vout in a transaction.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="trxid">The transaction ID as a hash string.</param>
        /// <param name="vout">The vout to get the unspent outputs for.</param>
        /// <param name="includeMemPool">A flag that specifies whether to include transactions in the mempool.</param>
        /// <returns>Json formatted <see cref="GetTxOutModel"/>. <c>null</c> if no unspent outputs given parameters. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if network or chain not provided.</exception>
        /// <exception cref="ArgumentException">Thrown if trxid is empty or not a valid <see cref="uint256"/></exception>
        [Route("gettxout")]
        [HttpGet]
        public IActionResult GetTxOut([FromQuery] string trxid, uint vout = 0, bool includeMemPool = true)
        {
            try
            {
                Guard.NotEmpty(trxid, nameof(trxid));

                uint256 txid;
                if (!uint256.TryParse(trxid, out txid))
                {
                    throw new ArgumentException(nameof(trxid));
                }

                UnspentOutputs unspentOutputs = null;
                if (includeMemPool)
                {
                    unspentOutputs = this.pooledGetUnspentTransaction != null ? this.pooledGetUnspentTransaction.GetUnspentTransaction(txid) : null;
                }
                else
                {
                    unspentOutputs = this.getUnspentTransaction != null ? this.getUnspentTransaction.GetUnspentTransaction(txid) : null;
                }

                if (unspentOutputs == null)
                {
                    return this.Json(null);
                }

                return this.Json(new GetTxOutModel(unspentOutputs, vout, this.network, this.chainIndexer.Tip));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Triggers a shutdown of this node.
        /// </summary>
        /// <param name="corsProtection">This body parameter is here to prevent a Cross Origin Resource Sharing
        /// (CORS) call from triggering method execution. CORS relaxes security and you can read more about this
        /// <a href="https://docs.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-2.1">here</a>.</param>
        /// <remarks>
        /// <seealso cref="https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS#Simple_requests"/>
        /// </remarks>
        /// <returns><see cref="OkResult"/></returns>
        [HttpPost]
        [Route("shutdown")]
        [Route("stop")]
        public IActionResult Shutdown([FromBody] bool corsProtection = true)
        {
            // Start the node shutdown process, by calling StopApplication, which will signal to
            // the full node RunAsync to continue processing, which calls Dispose on the node.
            this.fullNode?.NodeLifetime.StopApplication();

            return this.Ok();
        }

        /// <summary>
        /// Changes the log levels for the specified loggers.
        /// </summary>
        /// <param name="request">The request containing the loggers to modify.</param>
        /// <returns><see cref="OkResult"/></returns>
        [HttpPut]
        [Route("loglevels")]
        public IActionResult UpdateLogLevel([FromBody] LogRulesRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                foreach (LogRuleRequest logRuleRequest in request.LogRules)
                {
                    LogLevel nLogLevel = logRuleRequest.LogLevel.ToNLogLevel();
                    LoggingRule rule = LogManager.Configuration.LoggingRules.SingleOrDefault(r => r.LoggerNamePattern == logRuleRequest.RuleName);

                    if (rule == null)
                    {
                        throw new Exception($"Logger name `{logRuleRequest.RuleName}` doesn't exist.");
                    }

                    // Log level ordinals go from 1 to 6 (trace to fatal).
                    // When we set a log level, we enable every log level above and disable all the ones below.
                    foreach (LogLevel level in LogLevel.AllLoggingLevels)
                    {
                        if (level.Ordinal >= nLogLevel.Ordinal)
                        {
                            rule.EnableLoggingForLevel(level);
                        }
                        else
                        {
                            rule.DisableLoggingForLevel(level);
                        }
                    }
                }

                // Only update the loggers if the setting was successful.
                LogManager.ReconfigExistingLoggers();
                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Get the enabled log rules.
        /// </summary>
        /// <returns>A list of log rules.</returns>
        [HttpGet]
        [Route("logrules")]
        public IActionResult GetLogRules()
        {
            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var rules = new List<LogRuleModel>();

                foreach (LoggingRule rule in LogManager.Configuration.LoggingRules)
                {
                    string filename = string.Empty;

                    if (!rule.Targets.Any())
                    {
                        continue;
                    }

                    // Retrieve the full path of the current rule's log file.
                    if (rule.Targets.First().GetType().Name == "AsyncTargetWrapper")
                    {
                        WrapperTargetBase wrapper = (WrapperTargetBase)rule.Targets.First();

                        if (wrapper.WrappedTarget != null && wrapper.WrappedTarget.GetType().Name == "FileTarget")
                        {
                            filename = ((FileTarget)wrapper.WrappedTarget).FileName.ToString();
                        }
                    }
                    else if (rule.Targets.First().GetType().Name == "FileTarget")
                    {
                        filename = ((FileTarget)rule.Targets.First()).FileName.ToString();
                    }

                    rules.Add(new LogRuleModel
                    {
                        RuleName = rule.LoggerNamePattern,
                        LogLevel = rule.Levels.First().Name,
                        Filename = filename
                    });
                }

                return this.Json(rules);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Get the currently running async loops/delegates/tasks for diagnostic purposes.
        /// </summary>
        /// <returns>A list of running async loops/delegates/tasks.</returns>
        [HttpGet]
        [Route("asyncloops")]
        public IActionResult GetAsyncLoops()
        {
            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var loops = new List<AsyncLoopModel>();

                foreach ((string loopName, TaskStatus status) in this.asyncProvider.GetAll())
                {
                    loops.Add(new AsyncLoopModel() { LoopName = loopName, Status = Enum.GetName(typeof(TaskStatus), status)});
                }

                return this.Json(loops);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Retrieves a transaction block given a valid hash.
        /// This function is used by other methods in this class and not explicitly by RPC/API.
        /// </summary>
        /// <param name="trxid">A valid uint256 hash</param>
        /// <param name="fullNode">The full node. Used to access <see cref="IBlockStore"/>.</param>
        /// <param name="chain">The full node's chain. Used to get <see cref="ChainedHeader"/> block.</param>
        /// <returns>A <see cref="ChainedHeader"/> for the given transaction hash. Returns <c>null</c> if fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if fullnode is not provided.</exception>
        internal ChainedHeader GetTransactionBlock(uint256 trxid, IFullNode fullNode, ChainIndexer chain)
        {
            Guard.NotNull(fullNode, nameof(fullNode));

            ChainedHeader block = null;
            var blockStore = fullNode.NodeFeature<IBlockStore>();
            uint256 blockid = blockStore?.GetBlockIdByTransactionId(trxid);
            if (blockid != null)
            {
                block = chain?.GetHeader(blockid);
            }

            return block;
        }

        /// <summary>
        /// Retrieves the difficulty target of the full node's network.
        /// </summary>
        /// <param name="networkDifficulty">The network difficulty interface.</param>
        /// <returns>A network difficulty <see cref="NBitcoin.Target"/>. Returns <c>null</c> if fails.</returns>
        internal static Target GetNetworkDifficulty(INetworkDifficulty networkDifficulty = null)
        {
            return networkDifficulty?.GetNetworkDifficulty();
        }
    }
}
