using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Stratis.Bitcoin.Utilities
{
    public interface INodeStats
    {
        /// <summary>Registers action that will be used to append node stats when they are being collected.</summary>
        /// <param name="appendStatsAction">Action that will be invoked during stats collection.</param>
        /// <param name="statsType">Type of stats.</param>
        /// <param name="priority">Stats priority that will be used to determine invocation priority of stats collection.</param>
        void RegisterStats(Action<StringBuilder> appendStatsAction, StatsType statsType, int priority = 0);

        /// <summary>Collects inline stats and then feature stats.</summary>
        string GetStats();

        /// <summary>Collects benchmark stats.</summary>
        string GetBenchmark();
    }

    public class NodeStats : INodeStats
    {
        private List<StatsItem> stats;

        /// <summary>Protects access to <see cref="stats"/>.</summary>
        private readonly object locker;

        private readonly IDateTimeProvider dateTimeProvider;

        public NodeStats(IDateTimeProvider dateTimeProvider)
        {
            this.locker = new object();
            this.dateTimeProvider = dateTimeProvider;

            this.stats = new List<StatsItem>();
        }

        /// <inheritdoc />
        public void RegisterStats(Action<StringBuilder> appendStatsAction, StatsType statsType, int priority = 0)
        {
            lock (this.locker)
            {
                this.stats.Add(new StatsItem()
                {
                    AppendStatsAction = appendStatsAction,
                    StatsType = statsType,
                    Priority = priority
                });

                this.stats = this.stats.OrderByDescending(x => x.Priority).ToList();
            }
        }

        /// <inheritdoc />
        public string GetStats()
        {
            var statsBuilder = new StringBuilder();

            lock (this.locker)
            {
                string date = this.dateTimeProvider.GetUtcNow().ToString(CultureInfo.InvariantCulture);
                statsBuilder.AppendLine($"======Node stats====== {date}");

                foreach (StatsItem actionPriority in this.stats.Where(x => x.StatsType == StatsType.Inline))
                    actionPriority.AppendStatsAction(statsBuilder);

                foreach (StatsItem actionPriority in this.stats.Where(x => x.StatsType == StatsType.Component))
                    actionPriority.AppendStatsAction(statsBuilder);
            }

            return statsBuilder.ToString();
        }

        /// <inheritdoc />
        public string GetBenchmark()
        {
            var statsBuilder = new StringBuilder();

            lock (this.locker)
            {
                foreach (StatsItem actionPriority in this.stats.Where(x => x.StatsType == StatsType.Benchmark))
                    actionPriority.AppendStatsAction(statsBuilder);
            }

            return statsBuilder.ToString();
        }

        private struct StatsItem
        {
            public StatsType StatsType;

            public Action<StringBuilder> AppendStatsAction;

            public int Priority;
        }
    }

    public enum StatsType
    {
        /// <summary>
        /// Inline stats are usually single line stats that should
        /// display most important information about the node.
        /// </summary>
        Inline,

        /// <summary>
        /// Component-related stats are usually blocks of component specific stats.
        /// </summary>
        Component,

        /// <summary>
        /// Benchmarking stats that display performance related information.
        /// </summary>
        Benchmark
    }
}
