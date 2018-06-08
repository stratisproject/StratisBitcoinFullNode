using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusPerformanceSnapshot
    {
        public ConsensusPerformanceSnapshot(long processedInputs, long processedTransactions, long processedBlocks, long blockFetchingTime, long blockProcessingTime, long utxoFetchingTime)
        {
            this.TotalProcessedTransactions = processedTransactions;
            this.TotalProcessedInputs = processedInputs;
            this.TotalProcessedBlocks = processedBlocks;
            this.totalBlockFetchingTime = blockFetchingTime;
            this.totalBlockValidationTime = blockProcessingTime;
            this.totalUTXOFetchingTime = utxoFetchingTime;
        }

        private readonly long totalBlockFetchingTime;

        public TimeSpan TotalBlockFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this.totalBlockFetchingTime);
            }
        }

        private readonly long totalBlockValidationTime;

        public TimeSpan TotalBlockValidationTime
        {
            get
            {
                return TimeSpan.FromTicks(this.totalBlockValidationTime);
            }
        }

        private readonly long totalUTXOFetchingTime;

        public TimeSpan TotalUTXOFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this.totalUTXOFetchingTime);
            }
        }

        public long TotalProcessedBlocks { get; }

        public long TotalProcessedTransactions { get; }

        public long TotalProcessedInputs { get; set; }

        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        public ulong ProcessedBlocksPerSecond
        {
            get
            {
                return (ulong)((double)this.TotalProcessedBlocks / this.TotalBlockValidationTime.TotalSeconds);
            }
        }

        public ulong ProcessedInputsPerSecond
        {
            get
            {
                return (ulong)((double)this.TotalProcessedInputs / this.TotalBlockValidationTime.TotalSeconds);
            }
        }

        public ulong ProcessedTransactionsPerSecond
        {
            get
            {
                return (ulong)((double)this.TotalProcessedTransactions / this.TotalBlockValidationTime.TotalSeconds);
            }
        }

        public static ConsensusPerformanceSnapshot operator -(ConsensusPerformanceSnapshot end, ConsensusPerformanceSnapshot start)
        {
            if (end.Start != start.Start)
            {
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
            }
            if (end.Taken < start.Taken)
            {
                throw new InvalidOperationException("The difference of snapshot can't be negative");
            }
            return new ConsensusPerformanceSnapshot(end.TotalProcessedInputs - start.TotalProcessedInputs,
                                            end.TotalProcessedTransactions - start.TotalProcessedTransactions,
                                            end.TotalProcessedBlocks - start.TotalProcessedBlocks,
                                            end.totalBlockFetchingTime - start.totalBlockFetchingTime,
                                            end.totalBlockValidationTime - start.totalBlockValidationTime,
                                            end.totalUTXOFetchingTime - start.totalUTXOFetchingTime)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine("====Blk Fetching Speed====");
            if (this.TotalProcessedInputs > 0)
                builder.AppendLine("Inputs:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalBlockFetchingTime.TotalMilliseconds / this.TotalProcessedInputs).ToString("0.0000") + " ms/input");
            if (this.TotalProcessedTransactions > 0)
                builder.AppendLine("Transactions:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalBlockFetchingTime.TotalMilliseconds / this.TotalProcessedTransactions).ToString("0.0000") + " ms/tx");
            if (this.TotalProcessedBlocks > 0)
                builder.AppendLine("Blocks:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalBlockFetchingTime.TotalMilliseconds / this.TotalProcessedBlocks).ToString("0.0000") + " ms/block");
            builder.AppendLine("====Validation Speed====");
            if (this.TotalProcessedInputs > 0)
                builder.AppendLine("Inputs:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalBlockValidationTime.TotalMilliseconds / this.TotalProcessedInputs).ToString("0.0000") + " ms/inputs");
            if (this.TotalProcessedTransactions > 0)
                builder.AppendLine("Transactions:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalBlockValidationTime.TotalMilliseconds / this.TotalProcessedTransactions).ToString("0.0000") + " ms/tx");
            if (this.TotalProcessedBlocks > 0)
                builder.AppendLine("Blocks:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalBlockValidationTime.TotalMilliseconds / this.TotalProcessedBlocks).ToString("0.0000") + " ms/tx");
            builder.AppendLine("====UTXO Fetching Speed====");
            if (this.TotalProcessedInputs > 0)
                builder.AppendLine("Inputs:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalUTXOFetchingTime.TotalMilliseconds / this.TotalProcessedInputs).ToString("0.0000") + " ms/inputs");
            if (this.TotalProcessedTransactions > 0)
                builder.AppendLine("Transactions:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalUTXOFetchingTime.TotalMilliseconds / this.TotalProcessedTransactions).ToString("0.0000") + " ms/tx");
            if (this.TotalProcessedBlocks > 0)
                builder.AppendLine("Blocks:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalUTXOFetchingTime.TotalMilliseconds / this.TotalProcessedBlocks).ToString("0.0000") + " ms/tx");
            builder.AppendLine("====Speed breakdown(%)====");
            long total = this.totalBlockFetchingTime + this.totalUTXOFetchingTime + this.totalBlockValidationTime;
            if (total > 0)
            {
                builder.AppendLine("Blk Fetching:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.totalBlockFetchingTime * 100m / total).ToString("0.00") + " %");
                builder.AppendLine("Validation:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.totalBlockValidationTime * 100m / total).ToString("0.00") + " %");
                builder.AppendLine("Utxo Fetching:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.totalUTXOFetchingTime * 100m / total).ToString("0.00") + " %");
            }
            builder.AppendLine("==========================");
            return builder.ToString();
        }

        public static string ToTimespan(TimeSpan timespan)
        {
            return timespan.ToString("c");
        }

        public static string ToKBSec(ulong count)
        {
            return count + "/s";
        }

        public DateTime Start { get; set; }

        public DateTime Taken { get; set; }
    }

    public class ConsensusPerformanceCounter
    {
        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of date time functionality.</param>
        public ConsensusPerformanceCounter(IDateTimeProvider dateTimeProvider)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.Start = this.dateTimeProvider.GetUtcNow();
        }

        private long processedTransactions;

        public long ProcessedTransactions
        {
            get
            {
                return this.processedTransactions;
            }
        }

        public void AddProcessedTransactions(long count)
        {
            Interlocked.Add(ref this.processedTransactions, count);
        }

        public void AddProcessedInputs(long count)
        {
            Interlocked.Add(ref this.processedInputs, count);
        }

        public void AddProcessedBlocks(long count)
        {
            Interlocked.Add(ref this.processedBlocks, count);
        }

        public void AddUTXOFetchingTime(long count)
        {
            Interlocked.Add(ref this.uTXOFetchingTime, count);
        }

        private long uTXOFetchingTime;

        public TimeSpan UTXOFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this.uTXOFetchingTime);
            }
        }

        public void AddBlockProcessingTime(long count)
        {
            Interlocked.Add(ref this.blockProcessingTime, count);
        }

        private long blockProcessingTime;

        public TimeSpan BlockProcessingTime
        {
            get
            {
                return TimeSpan.FromTicks(this.blockFetchingTime);
            }
        }

        public void AddBlockFetchingTime(long count)
        {
            Interlocked.Add(ref this.blockFetchingTime, count);
        }

        private long blockFetchingTime;

        public TimeSpan BlockFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this.blockFetchingTime);
            }
        }

        private long processedInputs;

        public long ProcessedInputs
        {
            get
            {
                return this.processedInputs;
            }
        }

        private long processedBlocks;

        public long ProcessedBlocks
        {
            get
            {
                return this.processedBlocks;
            }
        }

        public ConsensusPerformanceSnapshot Snapshot()
        {
            var snap = new ConsensusPerformanceSnapshot(this.ProcessedInputs, this.ProcessedTransactions, this.ProcessedBlocks, this.blockFetchingTime, this.blockProcessingTime, this.uTXOFetchingTime)
            {
                Start = this.Start,
                Taken = this.dateTimeProvider.GetUtcNow()
            };
            return snap;
        }

        public DateTime Start { get; }

        public TimeSpan Elapsed
        {
            get
            {
                return this.dateTimeProvider.GetUtcNow() - this.Start;
            }
        }

        public override string ToString()
        {
            return this.Snapshot().ToString();
        }
    }
}
