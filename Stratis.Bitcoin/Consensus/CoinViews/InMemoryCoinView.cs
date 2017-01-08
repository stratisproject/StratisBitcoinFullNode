using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public class InMemoryCoinView : CoinView
	{
		ReaderWriterLock _Lock = new ReaderWriterLock();
		Dictionary<uint256, UnspentOutputs> _Unspents = new Dictionary<uint256, UnspentOutputs>();
		uint256 _BlockHash;

		public InMemoryCoinView()
		{
		}
		
		public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
		{
			if(txIds == null)
				throw new ArgumentNullException("txIds");
			using(_Lock.LockRead())
			{
				UnspentOutputs[] result = new UnspentOutputs[txIds.Length];
				for(int i = 0; i < txIds.Length; i++)
				{
					result[i] = _Unspents.TryGet(txIds[i]);
					if(result[i] != null)
						result[i] = result[i].Clone();
				}
				return Task.FromResult(new FetchCoinsResponse(result, _BlockHash));
			}
		}

		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			if(oldBlockHash == null)
				throw new ArgumentNullException("oldBlockHash");
			if(nextBlockHash == null)
				throw new ArgumentNullException("nextBlockHash");
			if(unspentOutputs == null)
				throw new ArgumentNullException("unspentOutputs");
			using(_Lock.LockWrite())
			{
				if(_BlockHash != null && oldBlockHash != _BlockHash)
					return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));
				_BlockHash = nextBlockHash;
				foreach(var unspent in unspentOutputs)
				{
					UnspentOutputs existing;
					if(_Unspents.TryGetValue(unspent.TransactionId, out existing))
					{
						existing.Spend(unspent);
					}
					else
					{
						existing = unspent.Clone();
						_Unspents.Add(unspent.TransactionId, existing);
					}
					if(existing.IsPrunable)
						_Unspents.Remove(unspent.TransactionId);
				}
			}
			return Task.FromResult(true);
		}

		public override Task<uint256> Rewind()
		{
			throw new NotImplementedException();
		}
	}
}
