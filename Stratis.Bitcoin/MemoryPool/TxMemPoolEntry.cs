﻿using System;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MemPoolException : Exception
	{
		public MemPoolException()
		{
		}

		public MemPoolException(string message) : base(message)
		{
		}
	}

	public struct LockPoints
	{
		// Will be set to the blockchain height and median time past
		// values that would be necessary to satisfy all relative locktime
		// raints (BIP68) of this tx given our view of block chain history
		public int Height;
		public long Time;
		// As long as the current chain descends from the highest height block
		// containing one of the inputs used in the calculation, then the cached
		// values are still valid even after a reorg.
		public ChainedBlock MaxInputBlock;
	};

	public class TxMemPoolEntry
	{
		public Transaction Transaction { get; private set; }
		public uint256 TransactionHash { get; private set; }

		public Money Fee { get; private set; } //!< Cached to avoid expensive parent-transaction lookups
		public long TxWeight { get; private set; } //!< ... and avoid recomputing tx weight (also used for GetTxSize())
		private long nModSize; //!< ... and modified size for priority
		private long nUsageSize; //!< ... and total memory usage
		public long Time { get; private set; } //!< Local time when entering the mempool
		private double entryPriority; //!< Priority when entering the mempool
		public int EntryHeight { get; private set; } //!< Chain height when entering the mempool
		public Money InChainInputValue { get; private set; } //!< Sum of all txin values that are already in blockchain
		public bool SpendsCoinbase { get; private set; } //!< keep track of transactions that spend a coinbase
		public long SigOpCost { get; private set; } //!< Total sigop cost
		private long feeDelta; //!< Used for determining the priority of the transaction for mining in a block
		public LockPoints LockPoints { get; private set; } //!< Track the height and time at which tx was final

		// Information about descendants of this transaction that are in the
		// mempool; if we remove this transaction we must remove all of these
		// descendants as well.  if nCountWithDescendants is 0, treat this entry as
		// dirty, and nSizeWithDescendants and nModFeesWithDescendants will not be
		// correct.
		public long CountWithDescendants { get; private set; } //!< number of descendant transactions
		public long SizeWithDescendants { get; private set; } //!< ... and size
		public Money ModFeesWithDescendants { get; private set; } //!< ... and total fees (all including us)

		// Analogous statistics for ancestor transactions
		public long CountWithAncestors { get; private set; }
		public long SizeWithAncestors { get; private set; }
		public Money ModFeesWithAncestors { get; private set; }
		public long SigOpCostWithAncestors { get; private set; }


		private const int SERIALIZE_TRANSACTION_NO_WITNESS = 0x40000000;

		private const int WITNESS_SCALE_FACTOR = 4;

		private static int GetTransactionWeight(Transaction tx)
		{
			
			return
				tx.GetSerializedSize(
					(ProtocolVersion) ((uint) ProtocolVersion.PROTOCOL_VERSION | SERIALIZE_TRANSACTION_NO_WITNESS),
					SerializationType.Network)*
				(WITNESS_SCALE_FACTOR - 1) +
				tx.GetSerializedSize(ProtocolVersion.PROTOCOL_VERSION, SerializationType.Network);
		}

		private int CalculateModifiedSize(int nTxSize)
		{
			// In order to avoid disincentivizing cleaning up the UTXO set we don't count
			// the constant overhead for each txin and up to 110 bytes of scriptSig (which
			// is enough to cover a compressed pubkey p2sh redemption) for priority.
			// Providing any more cleanup incentive than making additional inputs free would
			// risk encouraging people to create junk outputs to redeem later.
			if (nTxSize == 0)
				nTxSize = (GetTransactionWeight(this.Transaction) + WITNESS_SCALE_FACTOR - 1)/WITNESS_SCALE_FACTOR;

			foreach (var txInput in this.Transaction.Inputs)
			{
				var offset = 41U + Math.Min(110U, txInput.ScriptSig.Length);
				if (nTxSize > offset)
					nTxSize -= (int) offset;
			}
			return nTxSize;
		}

		public TxMemPoolEntry(Transaction transaction, Money nFee,
			long nTime, double entryPriority, int entryHeight,
			Money inChainInputValue, bool spendsCoinbase,
			long nSigOpsCost, LockPoints lp)
		{
			this.Transaction = transaction;
			this.TransactionHash = transaction.GetHash();
			this.Fee = nFee;
			this.Time = nTime;
			this.entryPriority = entryPriority;
			this.EntryHeight = entryHeight;
			this.InChainInputValue = inChainInputValue;
			this.SpendsCoinbase = spendsCoinbase;
			this.SigOpCost = nSigOpsCost;
			this.LockPoints = lp;

			this.TxWeight = GetTransactionWeight(transaction);
			nModSize = this.CalculateModifiedSize(this.Transaction.GetSerializedSize());
			//nUsageSize = RecursiveDynamicUsage(*tx) + memusage::DynamicUsage(Transaction);

			CountWithDescendants = 1;
			SizeWithDescendants = GetTxSize();
			ModFeesWithDescendants = Fee;
			Money nValueIn = transaction.TotalOut + Fee;
			Check.Assert(InChainInputValue <= nValueIn);

			feeDelta = 0;

			CountWithAncestors = 1;
			SizeWithAncestors = GetTxSize();
			ModFeesWithAncestors = Fee;
			SigOpCostWithAncestors = SigOpCost;
		}

		public TxMemPoolEntry(TxMemPoolEntry other)
		{
			throw new NotImplementedException();
		}

		/**
		 * Fast calculation of lower bound of current priority as update
		 * from entry priority. Only inputs that were originally in-chain will age.
		 */
		public double GetPriority(uint currentHeight)
		{
			double deltaPriority = ((double) (currentHeight - this.EntryHeight)*InChainInputValue.Satoshi)/nModSize;
			double dResult = entryPriority + deltaPriority;
			if (dResult < 0) // This should only happen if it was called with a height below entry height
				dResult = 0;
			return dResult;
		}

		public long GetTxSize()
		{
			return (long) this.Transaction.GetVirtualSize();
		}

		public long ModifiedFee => Fee + feeDelta;

		public long DynamicMemoryUsage()
		{
			return 1;
			//throw new NotImplementedException();
			//return nUsageSize;
		}

		// Adjusts the descendant state, if this entry is not dirty.
		public void UpdateDescendantState(long modifySize, Money modifyFee, long modifyCount)
		{
			SizeWithDescendants += modifySize;
			Check.Assert(SizeWithDescendants > 0);
			ModFeesWithDescendants += modifyFee;
			CountWithDescendants += modifyCount;
			Check.Assert(CountWithDescendants > 0);
		}

		// Adjusts the ancestor state
		public void UpdateAncestorState(long modifySize, Money modifyFee, long modifyCount, long modifySigOps)
		{
			SizeWithAncestors += modifySize;
			Check.Assert(SizeWithAncestors > 0);
			ModFeesWithAncestors += modifyFee;
			CountWithAncestors += modifyCount;
			Check.Assert(CountWithAncestors > 0);
			SigOpCostWithAncestors += modifySigOps;
			Check.Assert(SigOpCostWithAncestors >= 0);
		}

		// Updates the fee delta used for mining priority score, and the
		// modified fees with descendants.
		public void UpdateFeeDelta(long newFeeDelta)
		{
			ModFeesWithDescendants += newFeeDelta - feeDelta;
			ModFeesWithAncestors += newFeeDelta - feeDelta;
			feeDelta = newFeeDelta;
		}

		// Update the LockPoints after a reorg
		public void UpdateLockPoints(LockPoints lp)
		{
			this.LockPoints = lp;
		}

		public volatile uint vTxHashesIdx; //!< Index in mempool's vTxHashes

		public override string ToString()
		{
			return $"{this.TransactionHash} - {base.ToString()}";
		}
	}
}
