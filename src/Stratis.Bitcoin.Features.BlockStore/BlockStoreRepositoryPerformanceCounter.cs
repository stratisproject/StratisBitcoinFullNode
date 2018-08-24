﻿using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreRepositoryPerformanceCounter
    {
        private long repositoryInsertCount;

        private long repositoryDeleteCount;

        private long repositoryHitCount;

        private long repositoryMissCount;

        public string Name { get; private set; }

        public DateTime Start { get; private set; }

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public BlockStoreRepositoryPerformanceCounter(IDateTimeProvider dateTimeProvider, string name = "BlockStore")
        {
            this.Name = name;
            this.dateTimeProvider = dateTimeProvider;
            this.Start = this.dateTimeProvider.GetUtcNow();
        }

        public TimeSpan Elapsed
        {
            get
            {
                return this.dateTimeProvider.GetUtcNow() - this.Start;
            }
        }

        public long RepositoryInsertCount
        {
            get
            {
                return this.repositoryInsertCount;
            }
        }

        public long RepositoryDeleteCount
        {
            get
            {
                return this.repositoryDeleteCount;
            }
        }

        public long RepositoryHitCount
        {
            get
            {
                return this.repositoryHitCount;
            }
        }

        public long RepositoryMissCount
        {
            get
            {
                return this.repositoryMissCount;
            }
        }

        public void AddRepositoryHitCount(long count)
        {
            Interlocked.Add(ref this.repositoryHitCount, count);
        }

        public void AddRepositoryMissCount(long count)
        {
            Interlocked.Add(ref this.repositoryMissCount, count);
        }

        public void AddRepositoryDeleteCount(long count)
        {
            Interlocked.Add(ref this.repositoryDeleteCount, count);
        }

        public void AddRepositoryInsertCount(long count)
        {
            Interlocked.Add(ref this.repositoryInsertCount, count);
        }

        public BlockStoreRepositoryPerformanceSnapshot Snapshot()
        {
            var snap = new BlockStoreRepositoryPerformanceSnapshot(this.RepositoryHitCount, this.RepositoryMissCount, this.RepositoryDeleteCount, this.RepositoryInsertCount, this.Name)
            {
                Start = this.Start,
                Taken = this.dateTimeProvider.GetUtcNow()
            };
            return snap;
        }

        public override string ToString()
        {
            return this.Snapshot().ToString();
        }
    }

    public class BlockStoreRepositoryPerformanceSnapshot
    {
        public long TotalRepositoryHitCount { get; }

        public long TotalRepositoryMissCount { get; }

        public long TotalRepositoryDeleteCount { get; }

        public long TotalRepositoryInsertCount { get; }

        public string Name { get; private set; }

        public DateTime Start { get; set; }

        public DateTime Taken { get; set; }

        public BlockStoreRepositoryPerformanceSnapshot(long repositoryHitCount, long repositoryMissCount, long repositoryDeleteCount, long repositoryInsertCount, string name = "BlockStore")
        {
            this.TotalRepositoryHitCount = repositoryHitCount;
            this.TotalRepositoryMissCount = repositoryMissCount;
            this.TotalRepositoryDeleteCount = repositoryDeleteCount;
            this.TotalRepositoryInsertCount = repositoryInsertCount;
            this.Name = name;
        }

        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        public static BlockStoreRepositoryPerformanceSnapshot operator -(BlockStoreRepositoryPerformanceSnapshot end, BlockStoreRepositoryPerformanceSnapshot start)
        {
            if (end.Start != start.Start)
            {
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
            }
            if (end.Taken < start.Taken)
            {
                throw new InvalidOperationException("The difference of snapshot can't be negative");
            }
            return new BlockStoreRepositoryPerformanceSnapshot(end.TotalRepositoryHitCount - start.TotalRepositoryHitCount,
                                            end.TotalRepositoryMissCount - start.TotalRepositoryMissCount,
                                            end.TotalRepositoryDeleteCount - start.TotalRepositoryDeleteCount,
                                            end.TotalRepositoryInsertCount - start.TotalRepositoryInsertCount, start.Name)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine($"===={this.Name} Repository Stats(%)====");
            builder.AppendLine("Hit Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalRepositoryHitCount);
            builder.AppendLine("Miss Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalRepositoryMissCount);
            builder.AppendLine("Delete Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalRepositoryDeleteCount);
            builder.AppendLine("Insert Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalRepositoryInsertCount);

            long total = this.TotalRepositoryMissCount + this.TotalRepositoryHitCount;
            if (this.TotalRepositoryHitCount > 0 || this.TotalRepositoryMissCount > 0)
            {
                builder.AppendLine("Hit:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.TotalRepositoryHitCount * 100m / total).ToString("0.00") + " %");
                builder.AppendLine("Miss:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.TotalRepositoryMissCount * 100m / total).ToString("0.00") + " %");
            }
            builder.AppendLine("=================================");

            return builder.ToString();
        }
    }
}
