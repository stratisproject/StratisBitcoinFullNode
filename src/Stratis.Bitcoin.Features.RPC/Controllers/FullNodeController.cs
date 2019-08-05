using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
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

        /// <summary>A interface implementation for the initial block download state.</summary>
        private readonly IInitialBlockDownloadState ibdState;

        private readonly IStakeChain stakeChain;

        public FullNodeController(
            ILoggerFactory loggerFactory,
            IPooledTransaction pooledTransaction = null,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
            IGetUnspentTransaction getUnspentTransaction = null,
            INetworkDifficulty networkDifficulty = null,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ChainIndexer chainIndexer = null,
            IChainState chainState = null,
            Connection.IConnectionManager connectionManager = null,
            IConsensusManager consensusManager = null,
            IBlockStore blockStore = null,
            IInitialBlockDownloadState ibdState = null,
            IStakeChain stakeChain = null)
            : base(
                  fullNode: fullNode,
                  network: network,
                  nodeSettings: nodeSettings,
                  chainIndexer: chainIndexer,
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
            this.ibdState = ibdState;
            this.stakeChain = stakeChain;
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
        /// <param name="blockHash">The hash of the block in which to look for the transaction.</param>
        /// <returns>A <see cref="TransactionBriefModel"/> or <see cref="TransactionVerboseModel"/> as specified by verbose. <c>null</c> if no transaction matching the hash.</returns>
        /// <exception cref="ArgumentException">Thrown if txid is invalid uint256.</exception>"
        /// <remarks>When called with a blockhash argument, getrawtransaction will return the transaction if the specified block is available and the transaction is found in that block.
        /// When called without a blockhash argument, getrawtransaction will return the transaction if it is in the mempool, or if -txindex is enabled and the transaction is in a block in the blockchain.</remarks>
        [ActionName("getrawtransaction")]
        [ActionDescription("Gets a raw, possibly pooled, transaction from the full node.")]
        public async Task<TransactionModel> GetRawTransactionAsync(string txid, int verbose = 0, string blockHash = null)
        {
            Guard.NotEmpty(txid, nameof(txid));

            if (!uint256.TryParse(txid, out uint256 trxid))
            {
                throw new ArgumentException(nameof(trxid));
            }

            uint256 hash = null;
            if (!string.IsNullOrEmpty(blockHash) && !uint256.TryParse(blockHash, out hash))
            {
                throw new ArgumentException(nameof(blockHash));
            }

            // Special exception for the genesis block coinbase transaction.
            if (trxid == this.Network.GetGenesis().GetMerkleRoot().Hash)
            {
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "The genesis block coinbase is not considered an ordinary transaction and cannot be retrieved.");
            }

            Transaction trx = null;
            ChainedHeaderBlock chainedHeaderBlock = null;

            if (hash == null)
            {
                // Look for the transaction in the mempool, and if not found, look in the indexed transactions.
                trx = (this.pooledTransaction == null ? null : await this.pooledTransaction.GetTransaction(trxid).ConfigureAwait(false)) ??
                      this.blockStore.GetTransactionById(trxid);

                if (trx == null)
                {
                    throw new RPCServerException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "No such mempool transaction. Use -txindex to enable blockchain transaction queries.");
                }
            }
            else
            {
                // Retrieve the block specified by the block hash.
                chainedHeaderBlock = this.ConsensusManager.GetBlockData(hash);

                if (chainedHeaderBlock == null)
                {
                    throw new RPCServerException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Block hash not found.");
                }

                trx = chainedHeaderBlock.Block.Transactions.SingleOrDefault(t => t.GetHash() == trxid);

                if (trx == null)
                {
                    throw new RPCServerException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "No such transaction found in the provided block.");
                }
            }

            if (verbose != 0)
            {
                ChainedHeader block = chainedHeaderBlock != null ? chainedHeaderBlock.ChainedHeader : this.GetTransactionBlock(trxid);
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

            if (includeMemPool && this.pooledGetUnspentTransaction != null)
                unspentOutputs = await this.pooledGetUnspentTransaction.GetUnspentTransactionAsync(trxid).ConfigureAwait(false);

            if (!includeMemPool && this.getUnspentTransaction != null)
                unspentOutputs = await this.getUnspentTransaction.GetUnspentTransactionAsync(trxid).ConfigureAwait(false);

            if (unspentOutputs != null)
                return new GetTxOutModel(unspentOutputs, vout, this.Network, this.ChainIndexer.Tip);

            return null;
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
        /// <remarks>The binary format is not supported with RPC.</remarks>
        [ActionName("getblockheader")]
        [ActionDescription("Gets the block header of the block identified by the hash.")]
        public object GetBlockHeader(string hash, bool isJsonFormat = true)
        {
            Guard.NotNull(hash, nameof(hash));

            this.logger.LogDebug("RPC GetBlockHeader {0}", hash);

            if (this.ChainIndexer == null)
                return null;

            BlockHeader blockHeader = this.ChainIndexer.GetHeader(uint256.Parse(hash))?.Header;

            if (blockHeader == null)
                return null;

            if (isJsonFormat)
                return new BlockHeaderModel(blockHeader);

            return new HexModel(blockHeader.ToHex(this.Network));
        }

        /// <summary>
        /// Returns information about a bitcoin address and it's validity.
        /// </summary>
        /// <param name="address">The bech32 or base58 <see cref="BitcoinAddress"/> to validate.</param>
        /// <returns><see cref="ValidatedAddress"/> instance containing information about the bitcoin address and it's validity.</returns>
        /// <exception cref="ArgumentNullException">Thrown if address provided is null or empty.</exception>
        [ActionName("validateaddress")]
        [ActionDescription("Returns information about a bech32 or base58 bitcoin address")]
        public ValidatedAddress ValidateAddress(string address)
        {
            Guard.NotEmpty(address, nameof(address));

            var result = new ValidatedAddress
            {
                IsValid = false,
                Address = address,
            };

            try
            {
                // P2WPKH
                if (BitcoinWitPubKeyAddress.IsValid(address, this.Network, out Exception _))
                {
                    result.IsValid = true;
                }
                // P2WSH
                else if (BitcoinWitScriptAddress.IsValid(address, this.Network, out Exception _))
                {
                    result.IsValid = true;
                }
                // P2PKH
                else if (BitcoinPubKeyAddress.IsValid(address, this.Network))
                {
                    result.IsValid = true;
                }
                // P2SH
                else if (BitcoinScriptAddress.IsValid(address, this.Network))
                {
                    result.IsValid = true;
                    result.IsScript = true;
                }
            }
            catch (NotImplementedException)
            {
                result.IsValid = false;
            }

            if (result.IsValid)
            {
                var scriptPubKey = BitcoinAddress.Create(address, this.Network).ScriptPubKey;
                result.ScriptPubKey = scriptPubKey.ToHex();
                result.IsWitness = scriptPubKey.IsWitness(this.Network);
            }

            return result;
        }

        /// <summary>
        /// RPC method for returning a block.
        /// <para>
        /// Supports Json format by default, and optionally raw (hex) format by supplying <c>0</c> to <see cref="verbosity"/>.
        /// </para>
        /// </summary>
        /// <param name="blockHash">Hash of block to find.</param>
        /// <param name="verbosity">Defaults to 1. 0 for hex encoded data, 1 for a json object, and 2 for json object with transaction data.</param>
        /// <returns>The block according to format specified in <see cref="verbosity"/></returns>
        [ActionName("getblock")]
        [ActionDescription("Returns the block in hex, given a block hash.")]
        public object GetBlock(string blockHash, int verbosity = 1)
        {
            uint256 blockId = uint256.Parse(blockHash);

            // Does the block exist.
            ChainedHeader chainedHeader = this.ChainIndexer.GetHeader(blockId);

            if (chainedHeader == null)
                return null;

            Block block = chainedHeader.Block ?? this.blockStore?.GetBlock(blockId);

            // In rare occasions a block that is found in the
            // indexer may not have been pushed to the store yet. 
            if (block == null)
                return null;

            if (verbosity == 0)
                return new HexModel(block.ToHex(this.Network));

            var blockModel = new BlockModel(block, chainedHeader, this.ChainIndexer.Tip, this.Network, verbosity);

            if (this.Network.Consensus.IsProofOfStake)
            {
                var posBlock = block as PosBlock;

                blockModel.PosBlockSignature = posBlock.BlockSignature.ToHex(this.Network);
                blockModel.PosBlockTrust = new Target(chainedHeader.GetBlockProof()).ToUInt256().ToString();
                blockModel.PosChainTrust = chainedHeader.ChainWork.ToString(); // this should be similar to ChainWork

                if (this.stakeChain != null)
                {
                    BlockStake blockStake = this.stakeChain.Get(blockId);

                    blockModel.PosModifierv2 = blockStake?.StakeModifierV2.ToString();
                    blockModel.PosFlags = blockStake?.Flags == BlockFlag.BLOCK_PROOF_OF_STAKE ? "proof-of-stake" : "proof-of-work";
                    blockModel.PosHashProof = blockStake?.HashProof.ToString();
                }
            }

            return blockModel;
        }

        [ActionName("getnetworkinfo")]
        [ActionDescription("Returns an object containing various state info regarding P2P networking.")]
        public NetworkInfoModel GetNetworkInfo()
        {
            var networkInfoModel = new NetworkInfoModel
            {
                Version = this.FullNode?.Version?.ToUint() ?? 0,
                SubVersion = this.Settings?.Agent,
                ProtocolVersion = (uint)(this.Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                IsLocalRelay = this.ConnectionManager?.Parameters?.IsRelay ?? false,
                TimeOffset = this.ConnectionManager?.ConnectedPeers?.GetMedianTimeOffset() ?? 0,
                Connections = this.ConnectionManager?.ConnectedPeers?.Count(),
                IsNetworkActive = true,
                RelayFee = this.Settings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC) ?? 0,
                IncrementalFee = this.Settings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC) ?? 0 // set to same as min relay fee
            };

            var services = this.ConnectionManager?.Parameters?.Services;
            if (services != null)
            {
                networkInfoModel.LocalServices = Encoders.Hex.EncodeData(BitConverter.GetBytes((ulong)services));
            }

            return networkInfoModel;
        }

        [ActionName("getblockchaininfo")]
        [ActionDescription("Returns an object containing various state info regarding blockchain processing.")]
        public BlockchainInfoModel GetBlockchainInfo()
        {
            var blockchainInfo = new BlockchainInfoModel
            {
                Chain = this.Network?.Name,
                Blocks = (uint)(this.ChainState?.ConsensusTip?.Height ?? 0),
                Headers = (uint)(this.ChainIndexer?.Height ?? 0),
                BestBlockHash = this.ChainState?.ConsensusTip?.HashBlock,
                Difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0.0,
                MedianTime = this.ChainState?.ConsensusTip?.GetMedianTimePast().ToUnixTimeSeconds() ?? 0,
                VerificationProgress = 0.0,
                IsInitialBlockDownload = this.ibdState?.IsInitialBlockDownload() ?? true,
                Chainwork = this.ChainState?.ConsensusTip?.ChainWork,
                IsPruned = false
            };

            if (blockchainInfo.Headers > 0)
            {
                blockchainInfo.VerificationProgress = (double)blockchainInfo.Blocks / blockchainInfo.Headers;
            }

            return blockchainInfo;
        }

        private ChainedHeader GetTransactionBlock(uint256 trxid)
        {
            ChainedHeader block = null;

            uint256 blockid = this.blockStore?.GetBlockIdByTransactionId(trxid);
            if (blockid != null)
                block = this.ChainIndexer?.GetHeader(blockid);

            return block;
        }

        private Target GetNetworkDifficulty()
        {
            return this.networkDifficulty?.GetNetworkDifficulty();
        }
    }
}
