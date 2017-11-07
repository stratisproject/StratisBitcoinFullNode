using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusPerformanceSnapshot
    {
        public ConsensusPerformanceSnapshot(long processedInputs, long processedTransactions, long processedBlocks, long blockFetchingTime, long blockProcessingTime, long utxoFetchingTime)
        {
            this._TotalProcessedTransactions = processedTransactions;
            this._TotalProcessedInputs = processedInputs;
            this._TotalProcessedBlocks = processedBlocks;
            this._TotalBlockFetchingTime = blockFetchingTime;
            this._TotalBlockValidationTime = blockProcessingTime;
            this._TotalUTXOFetchingTime = utxoFetchingTime;
        }

        private readonly long _TotalBlockFetchingTime;
        public TimeSpan TotalBlockFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this._TotalBlockFetchingTime);
            }
        }

        private readonly long _TotalBlockValidationTime;
        public TimeSpan TotalBlockValidationTime
        {
            get
            {
                return TimeSpan.FromTicks(this._TotalBlockValidationTime);
            }
        }


        private readonly long _TotalUTXOFetchingTime;
        public TimeSpan TotalUTXOFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this._TotalUTXOFetchingTime);
            }
        }

        private readonly long _TotalProcessedBlocks;
        public long TotalProcessedBlocks
        {
            get
            {
                return this._TotalProcessedBlocks;
            }
        }

        private readonly long _TotalProcessedTransactions;
        public long TotalProcessedTransactions
        {
            get
            {
                return this._TotalProcessedTransactions;
            }
        }

        long _TotalProcessedInputs;
        public long TotalProcessedInputs
        {
            get
            {
                return this._TotalProcessedInputs;
            }
            set
            {
                this._TotalProcessedInputs = value;
            }
        }
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
            var total = this._TotalBlockFetchingTime + this._TotalUTXOFetchingTime + this._TotalBlockValidationTime;
            if (total > 0)
            {
                builder.AppendLine("Blk Fetching:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this._TotalBlockFetchingTime * 100m / total).ToString("0.00") + " %");
                builder.AppendLine("Validation:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this._TotalBlockValidationTime * 100m / total).ToString("0.00") + " %");
                builder.AppendLine("Utxo Fetching:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this._TotalUTXOFetchingTime * 100m / total).ToString("0.00") + " %");
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
        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of date time functionality.</param>
        public ConsensusPerformanceCounter(IDateTimeProvider dateTimeProvider)
        {
            this.dateTimeProvider = dateTimeProvider;
            this._Start = this.dateTimeProvider.GetUtcNow();
        }

        long _ProcessedTransactions;
        public long ProcessedTransactions
        {
            get
            {
                return this._ProcessedTransactions;
            }
        }

        public void AddProcessedTransactions(long count)
        {
            Interlocked.Add(ref this._ProcessedTransactions, count);
        }
        public void AddProcessedInputs(long count)
        {
            Interlocked.Add(ref this._ProcessedInputs, count);
        }
        public void AddProcessedBlocks(long count)
        {
            Interlocked.Add(ref this._ProcessedBlocks, count);
        }


        public void AddUTXOFetchingTime(long count)
        {
            Interlocked.Add(ref this._UTXOFetchingTime, count);
        }
        private long _UTXOFetchingTime;
        public TimeSpan UTXOFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this._UTXOFetchingTime);
            }
        }

        public void AddBlockProcessingTime(long count)
        {
            Interlocked.Add(ref this._BlockProcessingTime, count);
        }
        private long _BlockProcessingTime;
        public TimeSpan BlockProcessingTime
        {
            get
            {
                return TimeSpan.FromTicks(this._BlockFetchingTime);
            }
        }

        public void AddBlockFetchingTime(long count)
        {
            Interlocked.Add(ref this._BlockFetchingTime, count);
        }
        private long _BlockFetchingTime;
        public TimeSpan BlockFetchingTime
        {
            get
            {
                return TimeSpan.FromTicks(this._BlockFetchingTime);
            }
        }

        long _ProcessedInputs;
        public long ProcessedInputs
        {
            get
            {
                return this._ProcessedInputs;
            }
        }

        long _ProcessedBlocks;
        public long ProcessedBlocks
        {
            get
            {
                return this._ProcessedBlocks;
            }
        }

        public ConsensusPerformanceSnapshot Snapshot()
        {
            var snap = new ConsensusPerformanceSnapshot(this.ProcessedInputs, this.ProcessedTransactions, this.ProcessedBlocks, this._BlockFetchingTime, this._BlockProcessingTime, this._UTXOFetchingTime)
            {
                Start = this.Start,
                Taken = this.dateTimeProvider.GetUtcNow()
            };
            return snap;
        }

        DateTime _Start;
        public DateTime Start
        {
            get
            {
                return this._Start;
            }
        }
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
