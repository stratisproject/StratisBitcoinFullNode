using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MemepoolValidationState
	{
		public MemepoolError Error { get; set; }

		public MemepoolValidationState Fail(MemepoolError error)
		{
			this.Error = error;
			return this;
		}

		public bool MissingInputs { get; set; }

		public void Throw()
		{
			throw new MempoolErrorException(this);
		}
	}

	public class MempoolValidationContext
    {
		public MemepoolValidationState State { get; }

		public List<uint256> SetConflicts { get; set; }

		public Transaction Transaction { get; }

		public uint256 TransactionHash { get; }

		public TxMemPoolEntry Entry { get; set; }

		public MemPoolCoinView View { get; set; }

		public Money ModifiedFees { get; set; }

	    public int EntrySize { get; set; }

	    public TxMemPool.SetEntries AllConflicting { get; set; }

		public TxMemPool.SetEntries SetAncestors { get; set; }

		public Money ConflictingFees { get; set; }
		public long ConflictingSize { get; set; }
		public long ConflictingCount { get; set; }

		public MempoolValidationContext(Transaction transaction, MemepoolValidationState state)
		{
			this.Transaction = transaction;
			this.TransactionHash = transaction.GetHash();
			this.State = state;
		}
	}
}
