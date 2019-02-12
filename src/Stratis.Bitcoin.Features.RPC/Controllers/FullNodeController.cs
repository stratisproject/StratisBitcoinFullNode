using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    /// <summary>
    /// A <see cref="FeatureController"/> that implements several RPC methods for the full node.
    /// </summary>
    public class FullNodeController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An interface implementation used to retrieve a transaction.</summary>
        private readonly IPooledTransaction pooledTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        /// <summary>An interface implementation used to retrieve the network difficulty target.</summary>
        private readonly INetworkDifficulty networkDifficulty;

        /// <summary>An interface implementation for the blockstore.</summary>
        private readonly IBlockStore blockStore;

        public FullNodeController(
            ILoggerFactory loggerFactory,
            IPooledTransaction pooledTransaction = null,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
            IGetUnspentTransaction getUnspentTransaction = null,
            INetworkDifficulty networkDifficulty = null,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            Connection.IConnectionManager connectionManager = null,
            IConsensusManager consensusManager = null,
            IBlockStore blockStore = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  chain: chain,
                  chainState: chainState,
                  connectionManager: connectionManager,
                  consensusManager: consensusManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.pooledTransaction = pooledTransaction;
            this.pooledGetUnspentTransaction = pooledGetUnspentTransaction;
            this.getUnspentTransaction = getUnspentTransaction;
            this.networkDifficulty = networkDifficulty;
            this.blockStore = blockStore;
        }

        /// <summary>
        /// Stops the full node.
        /// </summary>
        [ActionName("stop")]
        [ActionDescription("Stops the full node.")]
        public Task Stop()
        {
            if (this.FullNode != null)
            {
                this.FullNode.NodeLifetime.StopApplication();
                this.FullNode = null;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves a transaction given a transaction hash in either simple or verbose form.
        /// </summary>
        /// <param name="txid">The transaction hash.</param>
        /// <param name="verbose">Non-zero if verbose model wanted.</param>
        /// <returns>A <see cref="TransactionBriefModel"/> or <see cref="TransactionVerboseModel"/> as specified by verbose. <c>null</c> if no transaction matching the hash.</returns>
        /// <exception cref="ArgumentException">Thrown if txid is invalid uint256.</exception>"
        /// <remarks>Requires txindex=1, otherwise only txes that spend or create UTXOs for a wallet can be returned.</remarks>
        [ActionName("getrawtransaction")]
        [ActionDescription("Gets a raw, possibly pooled, transaction from the full node.")]
        public async Task<TransactionModel> GetRawTransactionAsync(string txid, int verbose = 0)
        {
            uint256 trxid;
            if (!uint256.TryParse(txid, out trxid))
                throw new ArgumentException(nameof(txid));

            Transaction trx = this.pooledTransaction != null ? await this.pooledTransaction.GetTransaction(trxid) : null;

            if (trx == null)
            {
                trx = this.blockStore != null ? await this.blockStore.GetTransactionByIdAsync(trxid) : null;
            }

            if (trx == null)
                return null;

            if (verbose != 0)
            {
                ChainedHeader block = await this.GetTransactionBlockAsync(trxid);
                return new TransactionVerboseModel(trx, this.Network, block, this.ChainState?.ConsensusTip);
            }
            else
                return new TransactionBriefModel(trx);
        }

        /// <summary>
        /// Decodes a transaction from its raw hexadecimal format.
        /// </summary>
        /// <param name="hex">The raw transaction hex.</param>
        /// <returns>A <see cref="TransactionVerboseModel"/> or <c>null</c> if the transaction could not be decoded.</returns>
        [ActionName("decoderawtransaction")]
        [ActionDescription("Decodes a serialized transaction hex string into a JSON object describing the transaction.")]
        public TransactionModel DecodeRawTransaction(string hex)
        {
            try
            {
                return new TransactionVerboseModel(this.FullNode.Network.CreateTransaction(hex), this.Network);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Implements gettextout RPC call.
        /// </summary>
        /// <param name="txid">The transaction id.</param>
        /// <param name="vout">The vout number.</param>
        /// <param name="includeMemPool">Whether to include the mempool.</param>
        /// <returns>A <see cref="GetTxOutModel"/> containing the unspent outputs of the transaction id and vout. <c>null</c> if unspent outputs not found.</returns>
        /// <exception cref="ArgumentException">Thrown if txid is invalid.</exception>"
        [ActionName("gettxout")]
        [ActionDescription("Gets the unspent outputs of a transaction id and vout number.")]
        public async Task<GetTxOutModel> GetTxOutAsync(string txid, uint vout, bool includeMemPool = true)
        {
            uint256 trxid;
            if (!uint256.TryParse(txid, out trxid))
                throw new ArgumentException(nameof(txid));

            UnspentOutputs unspentOutputs = null;
            if (includeMemPool)
            {
                unspentOutputs = this.pooledGetUnspentTransaction != null ? await this.pooledGetUnspentTransaction.GetUnspentTransactionAsync(trxid) : null;
            }
            else
            {
                unspentOutputs = this.getUnspentTransaction != null ? await this.getUnspentTransaction.GetUnspentTransactionAsync(trxid) : null;
            }

            if (unspentOutputs == null)
                return null;

            return new GetTxOutModel(unspentOutputs, vout, this.Network, this.Chain.Tip);
        }

        /// <summary>
        /// Implements the getblockcount RPC call.
        /// </summary>
        /// <returns>The current consensus tip height.</returns>
        [ActionName("getblockcount")]
        [ActionDescription("Gets the current consensus tip height.")]
        public int GetBlockCount()
        {
            return this.ConsensusManager?.Tip.Height ?? -1;
        }

        /// <summary>
        /// Implements the getinfo RPC call.
        /// </summary>
        /// <returns>A <see cref="GetInfoModel"/> with information about the full node.</returns>
        [ActionName("getinfo")]
        [ActionDescription("Gets general information about the full node.")]
        public GetInfoModel GetInfo()
        {
            var model = new GetInfoModel
            {
                Version = this.FullNode?.Version?.ToUint() ?? 0,
                ProtocolVersion = (uint)(this.Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                Blocks = this.ChainState?.ConsensusTip?.Height ?? 0,
                TimeOffset = this.ConnectionManager?.ConnectedPeers?.GetMedianTimeOffset() ?? 0,
                Connections = this.ConnectionManager?.ConnectedPeers?.Count(),
                Proxy = string.Empty,
                Difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0,
                Testnet = this.Network.IsTest(),
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

            return model;
        }

        /// <summary>
        /// Implements getblockheader RPC call.
        /// </summary>
        /// <param name="hash">Hash of the block.</param>
        /// <param name="isJsonFormat">Indicates whether to provide data in Json or binary format.</param>
        /// <returns>The block header rpc format.</returns>
        /// <exception cref="NotImplementedException">Thrown if isJsonFormat = false</exception>
        /// <remarks>The binary format is not supported with RPC.</remarks>
        [ActionName("getblockheader")]
        [ActionDescription("Gets the block header of the block identified by the hash.")]
        public BlockHeaderModel GetBlockHeader(string hash, bool isJsonFormat = true)
        {
            Guard.NotNull(hash, nameof(hash));

            this.logger.LogDebug("RPC GetBlockHeader {0}", hash);

            if (!isJsonFormat)
            {
                this.logger.LogError("Binary serialization is not supported for RPC '{0}'.", nameof(this.GetBlockHeader));
                throw new NotImplementedException();
            }

            BlockHeaderModel model = null;
            if (this.Chain != null)
            {
                BlockHeader blockHeader = this.Chain.GetBlock(uint256.Parse(hash))?.Header;
                if (blockHeader != null)
                    model = new BlockHeaderModel(blockHeader);
            }

            return model;
        }

        /// <summary>
        /// Returns information about a bitcoin address
        /// </summary>
        /// <param name="address">bech32 or base58 BitcoinAddress to validate.</param>
        /// <returns>ValidatedAddress containing a boolean indicating address validity</returns>
        /// <exception cref="ArgumentNullException">Thrown if address provided is null/empty.</exception>
        [ActionName("validateaddress")]
        [ActionDescription("Returns information about a bech32 or base58 bitcoin address")]
        public ValidatedAddress ValidateAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentNullException("address");

            var res = new ValidatedAddress();
            res.IsValid = false;

            // P2WPKH
            if (BitcoinWitPubKeyAddress.IsValid(address, this.Network, out Exception _))
            {
                res.IsValid = true;
            }
            // P2WSH
            else if (BitcoinWitScriptAddress.IsValid(address, this.Network, out Exception _))
            {
                // We don't support P2WSH addresses yet
                res.IsValid = false;
            }
            // P2PKH
            else if (BitcoinPubKeyAddress.IsValid(address, this.Network))
            {
                res.IsValid = true;
            }
            // P2SH
            else if (BitcoinScriptAddress.IsValid(address, this.Network))
            {
                res.IsValid = true;
            }

            return res;
        }

        /// <summary>
        /// RPC method for returning a block.
        /// Supports Json format by default, and optionally raw (hex) format by supplying <c>false</c> to <see cref="isJsonFormat"/>.
        /// </summary>
        /// <param name="blockHash">Hash of block to find.</param>
        /// <param name="isJsonFormat">Whether to output in raw format or in Json format.</param>
        /// <returns>The block according to format specified in <see cref="isJsonFormat"/></returns>
        [ActionName("getblock")]
        [ActionDescription("Returns the block in hex, given a block hash.")]
        public async Task<object> GetBlockAsync(string blockHash, bool isJsonFormat = true)
        {
            Block block = this.blockStore != null ? await this.blockStore.GetBlockAsync(uint256.Parse(blockHash)).ConfigureAwait(false) : null;

            if (!isJsonFormat)
                return block;

            return new BlockModel(block, this.Chain);
        }

        private async Task<ChainedHeader> GetTransactionBlockAsync(uint256 trxid)
        {
            ChainedHeader block = null;

            uint256 blockid = this.blockStore != null ? await this.blockStore.GetBlockIdByTransactionIdAsync(trxid) : null;
            if (blockid != null)
                block = this.Chain?.GetBlock(blockid);

            return block;
        }

        private Target GetNetworkDifficulty()
        {
            return this.networkDifficulty?.GetNetworkDifficulty();
        }
    }
}
