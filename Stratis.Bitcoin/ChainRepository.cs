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
		DBreezeSingleThreadSession _Session;

		public ChainRepository(string folder)
		{
			_Session = new DBreezeSingleThreadSession("DBreeze ChainRepository", folder);
		}
		
		BlockLocator _Locator;
		public Task<ConcurrentChain> GetChain()
		{
			return _Session.Do(() =>
			{
				ChainedBlock tip = null;
				foreach(var row in _Session.Transaction.SelectForward<int, BlockHeader>("Chain"))
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
		}

		public Task Save(ConcurrentChain chain)
		{
			return _Session.Do(() =>
			{
				var fork = _Locator == null ? null : chain.FindFork(_Locator);
				var tip = chain.Tip;
				var toSave = tip;
				List<ChainedBlock> blocks = new List<ChainedBlock>();
				while(toSave != fork)
				{
					//DBreeze faster on ordered insert
					blocks.Insert(0, toSave);
					toSave = toSave.Previous;
				}				
				foreach(var block in blocks)
				{
					_Session.Transaction.Insert<int, BlockHeader>("Chain", block.Height, block.Header);
				}
				_Locator = tip.GetLocator();
				_Session.Transaction.Commit();
			});
		}

		public void Dispose()
		{
			_Session.Dispose();
		}
	}
}
