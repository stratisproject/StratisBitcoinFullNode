using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Utilities;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public class FullNodeController : BaseRPCController
    {
        private readonly ILogger logger;

        public FullNodeController(
            ILoggerFactory loggerFactory,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            PowConsensusValidator consensusValidator = null,
            ConcurrentChain chain = null,
            ChainState chainState = null,
            BlockStoreManager blockManager = null,
            MempoolManager mempoolManager = null,
            Connection.IConnectionManager connectionManager = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  consensusValidator: consensusValidator,
                  chain: chain,
                  chainState: chainState,
                  blockManager: blockManager,
                  mempoolManager: mempoolManager,
                  connectionManager: connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("stop")]
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
        public async Task<TransactionModel> GetRawTransaction(string txid, int verbose = 0)
        {
            uint256 trxid;
            if (!uint256.TryParse(txid, out trxid))
                throw new ArgumentException(nameof(txid));

            Transaction trx = (await this.MempoolManager?.InfoAsync(trxid))?.Trx;

            if (trx == null)
                trx = await this.BlockManager?.BlockRepository?.GetTrxAsync(trxid);

            if (trx == null)
                return null;

            if (verbose != 0)
            {
                ChainedBlock block = await this.GetTransactionBlock(trxid);
                return new TransactionVerboseModel(trx, this.Network, block, this.ChainState?.HighestValidatedPoW);
            }
            else
                return new TransactionBriefModel(trx);
        }

        [ActionName("getblockcount")]
        public int GetBlockCount()
        {
            var consensusLoop = this.FullNode.Services.ServiceProvider.GetRequiredService<ConsensusLoop>();
            return consensusLoop.Tip.Height;
        }

        [ActionName("getinfo")]
        public GetInfoModel GetInfo()
        {
            var model = new GetInfoModel()
            {
                version = this.FullNode?.Version.ToUint() ?? 0,
                protocolversion = (uint)(this.Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                blocks = this.ChainState?.HighestValidatedPoW?.Height ?? 0,
                timeoffset = this.ConnectionManager?.ConnectedNodes?.GetMedianTimeOffset() ?? 0,
                connections = this.ConnectionManager?.ConnectedNodes?.Count(),
                proxy = string.Empty,
                difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0,
                testnet = this.Network.IsTest(),
                relayfee = MempoolValidator.MinRelayTxFee.FeePerK.ToUnit(MoneyUnit.BTC),
                errors = string.Empty,

                //TODO: Wallet related infos: walletversion, balance, keypoololdest, keypoolsize, unlocked_until, paytxfee
                walletversion = null,
                balance = null,
                keypoololdest = null,
                keypoolsize = null,
                unlocked_until = null,
                paytxfee = null
            };


            return model;
        }

        /// <summary>
        /// Implements getblockheader RPC call
        /// </summary>
        /// <param name="hash">hash of block</param>
        /// <param name="isJsonFormat">indicates whether to provide data in Json or binary format</param>
        /// <returns>The block header rpc format</returns>
        [ActionName("getblockheader")]
        public BlockHeaderModel GetBlockHeader(string hash, bool isJsonFormat = true)
        {
            Guard.NotNull(this.Chain, nameof(this.Chain));

            this.logger.LogDebug("RPC GetBlockHeader {0}", hash);

            if (!isJsonFormat)
            {
                this.logger.LogError("Binary serialization is not supported for RPC {0}", nameof(GetBlockHeader));
                throw new NotImplementedException();
            }

            return new BlockHeaderModel(this.Chain?.GetBlock(uint256.Parse(hash))?.Header);
        }

        private async Task<ChainedBlock> GetTransactionBlock(uint256 trxid)
        {
            ChainedBlock block = null;
            uint256 blockid = await this.BlockManager?.BlockRepository?.GetTrxBlockIdAsync(trxid);
            if (blockid != null)
                block = this.Chain?.GetBlock(blockid);
            return block;
        }

        private Target GetNetworkDifficulty()
        {
            if (this.ConsensusValidator?.ConsensusParams != null && this.ChainState?.HighestValidatedPoW != null)
                return Miner.PowMining.GetWorkRequired(this.ConsensusValidator.ConsensusParams, this.ChainState?.HighestValidatedPoW);
            else
                return null;
        }
    }
}
