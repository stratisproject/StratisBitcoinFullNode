using DBreeze;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.Transactions;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin
{
	public class ChainRepository : IDisposable
	{
		private DBreezeEngine _Engine;
		private CustomThreadPoolTaskScheduler _SingleThread;
		private DBreeze.Transactions.Transaction _Transaction;

		public ChainRepository(string folder)
		{
			_SingleThread = new CustomThreadPoolTaskScheduler(1, 100, "DBreeze ChainRepository");
			new Task(() =>
			{
				DBreeze.Utils.CustomSerializator.ByteArraySerializator = DBreezeCoinView.NBitcoinSerialize;
				DBreeze.Utils.CustomSerializator.ByteArrayDeSerializator = DBreezeCoinView.NBitcoinDeserialize;
				_Engine = new DBreezeEngine(folder);
				_Transaction = _Engine.GetTransaction();
				_Transaction.SynchronizeTables("Chain");
				_Transaction.ValuesLazyLoadingIsOn = false;
			}).Start(_SingleThread);
		}

		public void Dispose()
		{
			new Task(() =>
			{
				if(_Transaction != null)
				{
					_Transaction.Dispose();
					_Transaction = null;
				}
				if(_Engine != null)
				{
					_Engine.Dispose();
					_Engine = null;
				}
			}).Start(_SingleThread);			
			_SingleThread.WaitFinished();
			if(_SingleThread != null)
			{
				_SingleThread.Dispose();
				_SingleThread = null;
			}
		}
		BlockLocator _Locator;
		public Task<ConcurrentChain> GetChain()
		{
			var task = new Task<ConcurrentChain>(() =>
			{
				ChainedBlock tip = null;
				foreach(var row in _Transaction.SelectForward<int, BlockHeader>("Chain"))
				{
					if(tip != null && row.Value.HashPrevBlock != tip.HashBlock)
						break;
					tip = new ChainedBlock(row.Value, null, tip);
				}
				if(tip == null)
					return null;
				_Locator = tip.GetLocator();
				var chain = new ConcurrentChain();
				chain.SetTip(tip);
				return chain;
			});
			task.Start(_SingleThread);
			return task;
		}

		public Task Save(ConcurrentChain chain)
		{
			var task = new Task(() =>
			{
				var fork = _Locator == null ? null : chain.FindFork(_Locator);
				var tip = chain.Tip;
				var toSave = tip;
				List<ChainedBlock> blocks = new List<ChainedBlock>();
				while(toSave != fork)
				{
					toSave = toSave.Previous;
				}
				//DBreeze faster on ordered insert
				blocks.Reverse();
				foreach(var block in blocks)
				{
					_Transaction.Insert<int, BlockHeader>("Chain", block.Height, block.Header);
				}
				_Locator = tip.GetLocator();
				_Transaction.Commit();
			});
			task.Start(_SingleThread);
			return task;
		}
	}
}
