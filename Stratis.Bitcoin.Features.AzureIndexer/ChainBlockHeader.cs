using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
	public static class ChainChangeEntryExtensions
	{
		public static void UpdateChain(this IEnumerable<ChainBlockHeader> entries, ChainBase chain)
		{
			Stack<ChainBlockHeader> toApply = new Stack<ChainBlockHeader>();
			foreach(var entry in entries)
			{
				var prev = chain.GetBlock(entry.Header.HashPrevBlock);
				if(prev == null)
					toApply.Push(entry);
				else
				{
					toApply.Push(entry);
					break;
				}
			}
			while(toApply.Count > 0)
			{
				var newTip = toApply.Pop();

				var chained = new ChainedBlock(newTip.Header, newTip.BlockId, chain.GetBlock(newTip.Header.HashPrevBlock));
				chain.SetTip(chained);
			}
		}
	}
	public class ChainBlockHeader
	{
		public uint256 BlockId
		{
			get;
			set;
		}

		public int Height
		{
			get;
			set;
		}
		public BlockHeader Header
		{
			get;
			set;
		}

		public override string ToString()
		{
			return Height + "-" + BlockId;
		}
	}
}
