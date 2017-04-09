using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.PlatformAbstractions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.RPC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Builder;

namespace Stratis.Bitcoin.RPC.Controllers
{
    //TODO: Need to be extensible, should be FullNodeController
    public partial class FullNodeController : Controller
    {
        private IFullNode _FullNode;
        private NodeSettings _Settings;
        private NBitcoin.Network _Network;
        private ConsensusValidator _ConsensusValidator;
        private NBitcoin.ChainBase _Chain;
        private ChainBehavior.ChainState _ChainState;
        private BlockStoreManager _BlockManager;
        private MempoolManager _MempoolManager;
        private Connection.ConnectionManager _ConnectionManager;

        public FullNodeController(
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            NBitcoin.Network network = null,
            Consensus.ConsensusValidator consensusValidator = null,
            NBitcoin.ConcurrentChain chain = null,
            ChainBehavior.ChainState chainState = null,
            BlockStoreManager blockManager = null,
            MempoolManager mempoolManager = null,
            Connection.ConnectionManager connectionManager = null)
        {
            this._FullNode = fullNode;
            this._Settings = nodeSettings;
            this._Network = network;
            this._ConsensusValidator = consensusValidator;
            this._Chain = chain;
            this._ChainState = chainState;
            this._BlockManager = blockManager;
            this._MempoolManager = mempoolManager;
            this._ConnectionManager = connectionManager;
        }

        [ActionName("stop")]
        public Task Stop()
        {
            if (this._FullNode != null)
            {
                this._FullNode.Dispose();
                this._FullNode = null;
            }
            return Task.CompletedTask;
        }

        [ActionName("getrawtransaction")]
        public async Task<TransactionModel> GetRawTransaction(string txid, int verbose = 0)
        {
            NBitcoin.uint256 trxid;
            if (!NBitcoin.uint256.TryParse(txid, out trxid))
                throw new ArgumentException(nameof(txid));

            NBitcoin.Transaction trx = (await this._MempoolManager?.InfoAsync(trxid))?.Trx;

            if (trx == null)
                trx = await this._BlockManager?.BlockRepository?.GetTrxAsync(trxid);

            if (trx == null)
                return null;

            if (verbose != 0)
            {
                NBitcoin.ChainedBlock block = await GetTransactionBlock(trxid);
                return new TransactionVerboseModel(trx, this._Network, block, this._ChainState?.HighestValidatedPoW);
            }
            else
                return new TransactionBriefModel(trx);
        }

        [ActionName("getinfo")]
        public GetInfoModel GetInfo()
        {
            var model = new GetInfoModel()
            {
                version = this._FullNode?.Version.ToUint() ?? 0,
                protocolversion = (uint)(this._Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                blocks = this._ChainState?.HighestValidatedPoW?.Height ?? 0,
                timeoffset = 0, // TODO: Calculate median time offset of connected nodes
                connections = this._ConnectionManager?.ConnectedNodes?.Count(),
                proxy = string.Empty,
                difficulty = GetNetworkDifficulty()?.Difficulty ?? 0,
                testnet = this._Network == NBitcoin.Network.TestNet,
                relayfee = MempoolValidator.MinRelayTxFee.FeePerK.ToUnit(NBitcoin.MoneyUnit.BTC),
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

        private async Task<NBitcoin.ChainedBlock> GetTransactionBlock(NBitcoin.uint256 trxid)
        {
            NBitcoin.ChainedBlock block = null;
            NBitcoin.uint256 blockid = await this._BlockManager?.BlockRepository?.GetTrxBlockIdAsync(trxid);
            if (blockid != null)
                block = this._Chain?.GetBlock(blockid);
            return block;
        }

        private NBitcoin.Target GetNetworkDifficulty()
        {
            if (this._ConsensusValidator?.ConsensusParams != null && this._ChainState?.HighestValidatedPoW != null)
                return Miner.Mining.GetWorkRequired(this._ConsensusValidator.ConsensusParams, this._ChainState?.HighestValidatedPoW);
            else
                return null;
        }
    }
}
