using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public class ConsensusPerformanceSnapshot
	{

		public ConsensusPerformanceSnapshot(long processedInputs, long processedTransactions, long processedBlocks, long blockFetchingTime, long blockProcessingTime, long utxoFetchingTime)
		{
			_TotalProcessedTransactions = processedTransactions;
			_TotalProcessedInputs = processedInputs;
			_TotalProcessedBlocks = processedBlocks;
			_TotalBlockFetchingTime = blockFetchingTime;
			_TotalBlockValidationTime = blockProcessingTime;
			_TotalUTXOFetchingTime = utxoFetchingTime;
		}

		private readonly long _TotalBlockFetchingTime;
		public TimeSpan TotalBlockFetchingTime
		{
			get
			{
				return TimeSpan.FromTicks(_TotalBlockFetchingTime);
			}
		}

		private readonly long _TotalBlockValidationTime;
		public TimeSpan TotalBlockValidationTime
		{
			get
			{
				return TimeSpan.FromTicks(_TotalBlockValidationTime);
			}
		}


		private readonly long _TotalUTXOFetchingTime;
		public TimeSpan TotalUTXOFetchingTime
		{
			get
			{
				return TimeSpan.FromTicks(_TotalUTXOFetchingTime);
			}
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
				return (ulong)((double)TotalProcessedBlocks / TotalBlockValidationTime.TotalSeconds);
			}
		}
		public ulong ProcessedInputsPerSecond
		{
			get
			{
				return (ulong)((double)TotalProcessedInputs / TotalBlockValidationTime.TotalSeconds);
			}
		}
		public ulong ProcessedTransactionsPerSecond
		{
			get
			{
				return (ulong)((double)TotalProcessedTransactions / TotalBlockValidationTime.TotalSeconds);
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
											end.TotalProcessedBlocks - start.TotalProcessedBlocks,
											end._TotalBlockFetchingTime - start._TotalBlockFetchingTime,
											end._TotalBlockValidationTime - start._TotalBlockValidationTime,
											end._TotalUTXOFetchingTime - start._TotalUTXOFetchingTime)
			{
				Start = start.Taken,
				Taken = end.Taken
			};
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendLine("====Overall Speed====");
			if(TotalProcessedInputs > 0)
				builder.AppendLine("Inputs:".PadRight(Logs.ColumnLength) + (Elapsed.TotalMilliseconds / TotalProcessedInputs).ToString("0.0000") + " ms/input");
			if(TotalProcessedTransactions > 0)
				builder.AppendLine("Transactions:".PadRight(Logs.ColumnLength) + (Elapsed.TotalMilliseconds / TotalProcessedTransactions).ToString("0.0000") + " ms/tx");
			if(TotalProcessedBlocks > 0)
				builder.AppendLine("Blocks:".PadRight(Logs.ColumnLength) + (Elapsed.TotalMilliseconds / TotalProcessedBlocks).ToString("0.0000") + " ms/block");
			builder.AppendLine("====Validation Speed====");
			if(TotalProcessedInputs > 0)
				builder.AppendLine("Inputs:".PadRight(Logs.ColumnLength) + (TotalBlockValidationTime.TotalMilliseconds / TotalProcessedInputs).ToString("0.0000") + " ms/inputs");
			if(TotalProcessedTransactions > 0)
				builder.AppendLine("Transactions:".PadRight(Logs.ColumnLength) + (TotalBlockValidationTime.TotalMilliseconds / TotalProcessedTransactions).ToString("0.0000") + " ms/tx");
			if(TotalProcessedBlocks > 0)
				builder.AppendLine("Blocks:".PadRight(Logs.ColumnLength) + (TotalBlockValidationTime.TotalMilliseconds / TotalProcessedBlocks).ToString("0.0000") + " ms/tx");
			builder.AppendLine("====Speed breakdown(%)====");
			var total = _TotalBlockFetchingTime + _TotalUTXOFetchingTime + _TotalBlockValidationTime;
			if(total > 0)
			{
				builder.AppendLine("Blk Fetching:".PadRight(Logs.ColumnLength) + ((decimal)_TotalBlockFetchingTime * 100m / total).ToString("0.00") + " %");
				builder.AppendLine("Validation:".PadRight(Logs.ColumnLength) + ((decimal)_TotalBlockValidationTime * 100m / total).ToString("0.00") + " %");
				builder.AppendLine("Utxo Fetching:".PadRight(Logs.ColumnLength) + ((decimal)_TotalUTXOFetchingTime * 100m / total).ToString("0.00") + " %");
			}
			builder.AppendLine("==========================");
			return builder.ToString();
		}

		internal static string ToTimespan(TimeSpan timespan)
		{
			return timespan.ToString("c");
		}

		internal static string ToKBSec(ulong count)
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


		public void AddUTXOFetchingTime(long count)
		{
			Interlocked.Add(ref _UTXOFetchingTime, count);
		}
		private long _UTXOFetchingTime;
		public TimeSpan UTXOFetchingTime
		{
			get
			{
				return TimeSpan.FromTicks(_UTXOFetchingTime);
			}
		}

		public void AddBlockProcessingTime(long count)
		{
			Interlocked.Add(ref _BlockProcessingTime, count);
		}
		private long _BlockProcessingTime;
		public TimeSpan BlockProcessingTime
		{
			get
			{
				return TimeSpan.FromTicks(_BlockFetchingTime);
			}
		}

		public void AddBlockFetchingTime(long count)
		{
			Interlocked.Add(ref _BlockFetchingTime, count);
		}
		private long _BlockFetchingTime;
		public TimeSpan BlockFetchingTime
		{
			get
			{
				return TimeSpan.FromTicks(_BlockFetchingTime);
			}
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
			var snap = new ConsensusPerformanceSnapshot(ProcessedInputs, ProcessedTransactions, ProcessedBlocks, _BlockFetchingTime, _BlockProcessingTime, _UTXOFetchingTime)
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
	}
}
