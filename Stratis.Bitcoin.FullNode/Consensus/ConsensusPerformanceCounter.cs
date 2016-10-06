using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class ConsensusPerformanceSnapshot
	{

		public ConsensusPerformanceSnapshot(long processedInputs, long processedTransactions, long processedBlocks)
		{
			_TotalProcessedTransactions = processedTransactions;
			_TotalProcessedInputs = processedInputs;
			_TotalProcessedBlocks = processedBlocks;
		}


		private readonly long _TotalProcessedBlocks;
		public long TotalProcessedBlocks
		{
			get
			{
				return _TotalProcessedBlocks;
			}
		}

		private readonly long _TotalProcessedTransactions;
		public long TotalProcessedTransactions
		{
			get
			{
				return _TotalProcessedTransactions;
			}
		}

		long _TotalProcessedInputs;
		public long TotalProcessedInputs
		{
			get
			{
				return _TotalProcessedInputs;
			}
			set
			{
				_TotalProcessedInputs = value;
			}
		}
		public TimeSpan Elapsed
		{
			get
			{
				return Taken - Start;
			}
		}

		public ulong ProcessedBlocksPerSecond
		{
			get
			{
				return (ulong)((double)TotalProcessedBlocks / Elapsed.TotalSeconds);
			}
		}
		public ulong ProcessedInputsPerSecond
		{
			get
			{
				return (ulong)((double)TotalProcessedInputs / Elapsed.TotalSeconds);
			}
		}
		public ulong ProcessedTransactionsPerSecond
		{
			get
			{
				return (ulong)((double)TotalProcessedTransactions / Elapsed.TotalSeconds);
			}
		}

		public static ConsensusPerformanceSnapshot operator -(ConsensusPerformanceSnapshot end, ConsensusPerformanceSnapshot start)
		{
			if(end.Start != start.Start)
			{
				throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
			}
			if(end.Taken < start.Taken)
			{
				throw new InvalidOperationException("The difference of snapshot can't be negative");
			}
			return new ConsensusPerformanceSnapshot(end.TotalProcessedInputs - start.TotalProcessedInputs,
											end.TotalProcessedTransactions - start.TotalProcessedTransactions,
											end.TotalProcessedBlocks - start.TotalProcessedBlocks)
			{
				Start = start.Taken,
				Taken = end.Taken
			};
		}

		public override string ToString()
		{
			return "Inputs : " + ToKBSec(ProcessedInputsPerSecond) + ", Transactions : " + ToKBSec(ProcessedTransactionsPerSecond) + ", Blocks : " + ToKBSec(ProcessedBlocksPerSecond);
		}

		private string ToKBSec(ulong count)
		{
			return count + "/s";
		}

		public DateTime Start
		{
			get;
			set;
		}

		public DateTime Taken
		{
			get;
			set;
		}
	}
	public class ConsensusPerformanceCounter
	{
		public ConsensusPerformanceCounter()
		{
			_Start = DateTime.UtcNow;
		}

		long _ProcessedTransactions;
		public long ProcessedTransactions
		{
			get
			{
				return _ProcessedTransactions;
			}
		}

		public void AddProcessedTransactions(long count)
		{
			Interlocked.Add(ref _ProcessedTransactions, count);
		}
		public void AddProcessedInputs(long count)
		{
			Interlocked.Add(ref _ProcessedInputs, count);
		}
		public void AddProcessedBlocks(long count)
		{
			Interlocked.Add(ref _ProcessedBlocks, count);
		}

		long _ProcessedInputs;
		public long ProcessedInputs
		{
			get
			{
				return _ProcessedInputs;
			}
		}

		long _ProcessedBlocks;
		public long ProcessedBlocks
		{
			get
			{
				return _ProcessedBlocks;
			}
		}

		public ConsensusPerformanceSnapshot Snapshot()
		{
#if !(PORTABLE || NETCORE)
			Thread.MemoryBarrier();
#endif
			var snap = new ConsensusPerformanceSnapshot(ProcessedInputs, ProcessedTransactions, ProcessedBlocks)
			{
				Start = Start,
				Taken = DateTime.UtcNow
			};
			return snap;
		}

		DateTime _Start;
		public DateTime Start
		{
			get
			{
				return _Start;
			}
		}
		public TimeSpan Elapsed
		{
			get
			{
				return DateTime.UtcNow - Start;
			}
		}

		public override string ToString()
		{
			return Snapshot().ToString();
		}

		internal void Add(ConsensusPerformanceCounter counter)
		{
			AddProcessedTransactions(counter.ProcessedTransactions);
			AddProcessedInputs(counter.ProcessedInputs);
		}
	}
}
