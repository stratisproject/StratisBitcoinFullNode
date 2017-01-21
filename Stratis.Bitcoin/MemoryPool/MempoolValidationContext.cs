using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MemepoolValidationState
	{
		public MemepoolValidationState(bool limitFree, bool overrideMempoolLimit, Money absurdFee)
		{
			this.LimitFree = limitFree;
			this.AbsurdFee = absurdFee;
			this.OverrideMempoolLimit = overrideMempoolLimit;
		}

		public MempoolError Error { get; set; }

		public MemepoolValidationState Fail(MempoolError error)
		{
			this.Error = error;
			return this;
		}

		public Money AbsurdFee { get; set; }

		public bool MissingInputs { get; set; }

		public bool OverrideMempoolLimit { get; set; }

		public long AcceptTime { get; set; }

		public bool LimitFree { get; set; }

		public void Throw()
		{
			throw new MempoolErrorException(this);
		}
	}

	/// <summary>
	/// A context to hold validation data when adding
	/// a transaction to the memory pool.
	/// </summary>
	public class MempoolValidationContext
    {
		public MemepoolValidationState State { get; }

		public List<uint256> SetConflicts { get; set; }

		public Transaction Transaction { get; }

		public uint256 TransactionHash { get; }

		public TxMemPoolEntry Entry { get; set; }

		public MemPoolCoinView View { get; set; }

		public int EntrySize { get; set; }

		public TxMemPool.SetEntries AllConflicting { get; set; }

		public TxMemPool.SetEntries SetAncestors { get; set; }

		public LockPoints LockPoints { get; set; }

		public Money ConflictingFees { get; set; }
		public long ConflictingSize { get; set; }
		public long ConflictingCount { get; set; }

		public Money ValueOut { get; set; }
		public Money Fees { get; set; }
		public Money ModifiedFees { get; set; }
		public long SigOpsCost { get; set; }

		public MempoolValidationContext(Transaction transaction, MemepoolValidationState state)
		{
			this.Transaction = transaction;
			this.TransactionHash = transaction.GetHash();
			this.State = state;
		}
	}
}
