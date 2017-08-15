using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.IndexStore;
using Stratis.Bitcoin.Features.RPC.Models;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public class IndexStoreRPCController : BaseRPCController
    {
        private readonly ILogger logger;
        protected IndexStoreManager IndexManager;

        public IndexStoreRPCController(
            ILoggerFactory loggerFactory,
            IndexStoreManager indexManager,
            MempoolManager mempoolManager = null)
            : base(mempoolManager:mempoolManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.IndexManager = indexManager;
        }

        [ActionName("createindex")]
        public async Task<bool>CreateIndex(string name, bool multiValue, string builder, string[] dependancies = null)
        {
            if (dependancies?[0] == null)
                dependancies = null;

            return await this.IndexManager.IndexRepository.CreateIndex(name, multiValue, builder, dependancies);
        }

        [ActionName("dropindex")]
        public async Task<bool> DropIndex(string name)
        {
            return await this.IndexManager.IndexRepository.DropIndex(name);
        }

        [ActionName("listindexnames")]
        public string[] ListIndexNames()
        {
            return this.IndexManager.IndexRepository.Indexes.Keys.ToArray();
        }

        [ActionName("describeindex")]
        public string[] DescribeIndex(string name)
        {
            if (!this.IndexManager.IndexRepository.Indexes.TryGetValue(name, out Index index))
                return null;

            return new string[] { index.ToString() };
        }

        [ActionName("getrawtransaction")]
        public async Task<TransactionModel> GetRawTransaction(string txid, int verbose = 0)
        {
            uint256 trxid;
            if (!uint256.TryParse(txid, out trxid))
                throw new ArgumentException(nameof(txid));

            Transaction trx = (await this.MempoolManager?.InfoAsync(trxid))?.Trx;

            if (trx == null)
                trx = await this.IndexManager?.BlockRepository?.GetTrxAsync(trxid);

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

        private async Task<ChainedBlock> GetTransactionBlock(uint256 trxid)
        {
            ChainedBlock block = null;
            uint256 blockid = await this.IndexManager?.BlockRepository?.GetTrxBlockIdAsync(trxid);
            if (blockid != null)
                block = this.Chain?.GetBlock(blockid);
            return block;
        }
    }
}
