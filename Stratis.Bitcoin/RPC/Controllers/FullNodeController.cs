using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.RPC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC.Controllers
{
	//TODO: Need to be extensible, should be FullNodeController
	public partial class ConsensusController : Controller
	{
		[ActionName("stop")]
		public Task Stop()
		{
			_FullNode.Dispose();
			return Task.CompletedTask;
		}

		[ActionName("getrawtransaction")]
		public async Task<TransactionModel> GetRawTransaction(string txid, int verbose = 0)
		{
			uint256 trxid;
			if (!NBitcoin.uint256.TryParse(txid, out trxid))
				throw new ArgumentException(nameof(txid));

			ChainedBlock block = null;
			Transaction trx = (await _FullNode.MempoolManager.InfoAsync(trxid))?.Trx;
			if (trx == null)
			{
				var blockid = await _FullNode.BlockStoreManager.BlockRepository.GetTrxBlockIdAsync(trxid);
				if (blockid != null)
				{
					block = _FullNode.Chain.GetBlock(blockid);
					trx = await _FullNode.BlockStoreManager.BlockRepository.GetTrxAsync(trxid);
				}
			}

			if (trx == null)
			{
				return null;
			}

			if (verbose != 0)
				return new TransactionVerboseModel(trx, _FullNode.Network, block, _FullNode.ConsensusLoop.Tip);
			else
				return new TransactionBriefModel(trx);

		}

	}
}
