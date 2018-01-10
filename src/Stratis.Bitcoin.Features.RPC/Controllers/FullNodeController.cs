using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public class FullNodeController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public FullNodeController(
            ILoggerFactory loggerFactory,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            ChainState chainState = null,
            Connection.IConnectionManager connectionManager = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  chain: chain,
                  chainState: chainState,
                  connectionManager: connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("stop")]
        [ActionDescription("Stops the full node.")]
        public Task Stop()
        {
            if (this.FullNode != null)
            {
                this.FullNode.Dispose();
                this.FullNode = null;
            }
            return Task.CompletedTask;
        }

        [ActionName("getrawtransaction")]
        [ActionDescription("Gets a raw, possibly pooled, transaction from the full node.")]
        public async Task<TransactionModel> GetRawTransactionAsync(string txid, int verbose = 0)
        {
            uint256 trxid;
            if (!uint256.TryParse(txid, out trxid))
                throw new ArgumentException(nameof(txid));

            Transaction trx = await this.FullNode.NodeService<IPooledTransaction>(true)?.GetTransaction(trxid);

            if (trx == null)
            {
                trx = await this.FullNode.NodeFeature<IBlockStore>()?.GetTrxAsync(trxid);
            }

            if (trx == null)
                return null;

            if (verbose != 0)
            {
                ChainedBlock block = await this.GetTransactionBlockAsync(trxid);
                return new TransactionVerboseModel(trx, this.Network, block, this.ChainState?.ConsensusTip);
            }
            else
                return new TransactionBriefModel(trx);
        }

        /// <summary>
        /// Implements gettextout RPC call.
        /// </summary>
        /// <param name="txid">The transaction id</param>
        /// <param name="vout">The vout number</param>
        /// <param name="includeMemPool">Whether to include the mempool</param>
        /// <returns>The GetTxOut rpc format</returns>
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
                unspentOutputs = await this.FullNode.NodeService<IPooledGetUnspentTransaction>()?.GetUnspentTransactionAsync(trxid);
            }
            else
            {
                unspentOutputs = await this.FullNode.NodeService<IGetUnspentTransaction>()?.GetUnspentTransactionAsync(trxid);
            }

            if (unspentOutputs == null)
            {
                return null;
            }
            return new GetTxOutModel(unspentOutputs, vout, this.Network, this.Chain.Tip);
        }

        [ActionName("getblockcount")]
        [ActionDescription("Gets the current consensus tip height.")]
        public int GetBlockCount()
        {
            var consensusLoop = this.FullNode.Services.ServiceProvider.GetRequiredService<ConsensusLoop>();
            return consensusLoop.Tip.Height;
        }

        [ActionName("getinfo")]
        [ActionDescription("Gets general information about the full node.")]
        public GetInfoModel GetInfo()
        {
            var model = new GetInfoModel
            {
                Version = this.FullNode?.Version.ToUint() ?? 0,
                ProtocolVersion = (uint)(this.Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                Blocks = this.ChainState?.ConsensusTip?.Height ?? 0,
                TimeOffset = this.ConnectionManager?.ConnectedPeers?.GetMedianTimeOffset() ?? 0,
                Connections = this.ConnectionManager?.ConnectedPeers?.Count(),
                Proxy = string.Empty,
                Difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0,
                Testnet = this.Network.IsTest(),
                RelayFee = this.Settings.MinRelayTxFeeRate.FeePerK.ToUnit(MoneyUnit.BTC),
                Errors = string.Empty,

                //TODO: Wallet related infos: walletversion, balance, keypoololdest, keypoolsize, unlocked_until, paytxfee
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
        /// <param name="hash">Hash of block.</param>
        /// <param name="isJsonFormat">Indicates whether to provide data in Json or binary format.</param>
        /// <returns>The block header rpc format.</returns>
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
                model = new BlockHeaderModel(this.Chain.GetBlock(uint256.Parse(hash))?.Header);
            }
            return model;
        }

        /// <summary>
        /// Return information about <address>.
        /// </summary>
        /// <param name="address">BitcoinAddress to validate.</param>
        /// <returns>information about <address>. {isvalid}</returns>
        [ActionName("validateaddress")]
        [ActionDescription("Returns information about a bitcoin address")]
        public JObject ValidateAddress(string address)
        {
            if(address == null)
                throw new ArgumentNullException("address");

            JObject res = new JObject();
            res["isvalid"] = false; // until proven otherwise

            // P2PKH
            if(BitcoinPubKeyAddress.IsValid(address, ref this.Network))
            {
                res["isvalid"] = true;
            }
            // P2SH
            else if(BitcoinScriptAddress.IsValid(address, ref this.Network))
            {
                res["isvalid"] = true;
            }
            // P2WPKH
            else if (BitcoinWitPubKeyAddress.IsValid(address, ref this.Network))
            {
                res ["isvalid"] = true;
            }
            // P2WSH
            else if (BitcoinWitScriptAddress.IsValid(address, ref this.Network))
            {
                res ["isvalid"] = true;
            }
            return res;
        }

        private async Task<ChainedBlock> GetTransactionBlockAsync(uint256 trxid)
        {
            ChainedBlock block = null;
            uint256 blockid = await this.FullNode.NodeFeature<IBlockStore>()?.GetTrxBlockIdAsync(trxid);
            if (blockid != null)
                block = this.Chain?.GetBlock(blockid);
            return block;
        }

        private Target GetNetworkDifficulty()
        {
            return this.FullNode.NodeService<INetworkDifficulty>(true)?.GetNetworkDifficulty();
        }
    }
}
