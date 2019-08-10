using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Utilities
{
    public interface INodeStats
    {
        /// <summary>Registers action that will be used to append node stats when they are being collected.</summary>
        /// <param name="appendStatsAction">Action that will be invoked during stats collection.</param>
        /// <param name="statsType">Type of stats.</param>
        /// <param name="componentName">The component name.</param>
        /// <param name="priority">Stats priority that will be used to determine invocation priority of stats collection.</param>
        void RegisterStats(Action<StringBuilder> appendStatsAction, StatsType statsType, string componentName, int priority = 0);

        /// <summary>
        /// Removes stats previously registered.
        /// </summary>
        /// <param name="statsType">Type of stats.</param>
        /// <param name="componentName">The component name.</param>
        void RemoveStats(StatsType statsType, string componentName);

        /// <summary>Collects inline stats and then feature stats.</summary>
        string GetStats();

        /// <summary>Collects benchmark stats.</summary>
        string GetBenchmark();
    }

    public class NodeStats : INodeStats
    {
        // The amount of seconds the period loop will wait on a component to return it's stats before cancelling.
        private const int ComponentStatsWaitSeconds = 10;

        /// <summary>Protects access to <see cref="stats"/>.</summary>
        private readonly object locker;

        private readonly IDateTimeProvider dateTimeProvider;

        private readonly ILogger logger;

        private List<StatsItem> stats;

        public NodeStats(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.locker = new object();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stats = new List<StatsItem>();
        }

        /// <inheritdoc />
        public void RegisterStats(Action<StringBuilder> appendStatsAction, StatsType statsType, string componentName, int priority = 0)
        {
            lock (this.locker)
            {
                this.stats.Add(new StatsItem()
                {
                    AppendStatsAction = appendStatsAction,
                    ComponentName = componentName,
                    Priority = priority,
                    StatsType = statsType
                });

                this.stats = this.stats.OrderByDescending(x => x.Priority).ToList();
            }
        }

        /// <inheritdoc />
        public void RemoveStats(StatsType statsType, string componentName)
        {
            lock (this.locker)
            {
                this.stats.Remove(this.stats.Where(s => s.StatsType == statsType && s.ComponentName == componentName).FirstOrDefault());
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

                foreach (StatsItem inlineStatItem in this.stats.Where(x => x.StatsType == StatsType.Inline))
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ComponentStatsWaitSeconds)))
                        {
                            Task.Run(() =>
                            {
                                inlineStatItem.AppendStatsAction(statsBuilder);
                            }).WithCancellationAsync(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        this.logger.LogWarning("{0} failed to provide inline statistics after {1} seconds, please investigate...", inlineStatItem.ComponentName, ComponentStatsWaitSeconds);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError("{0} failed to provide inline statistics: {1}", inlineStatItem.ComponentName, ex.ToString());
                    }
                }

                foreach (StatsItem componentStatItem in this.stats.Where(x => x.StatsType == StatsType.Component))
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ComponentStatsWaitSeconds)))
                        {
                            Task.Run(() =>
                            {
                                componentStatItem.AppendStatsAction(statsBuilder);
                            }).WithCancellationAsync(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        this.logger.LogWarning("{0} failed to provide statistics after {1} seconds, please investigate...", componentStatItem.ComponentName, ComponentStatsWaitSeconds);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError("{0} failed to provide statistics: {1}", componentStatItem.ComponentName, ex.ToString());
                    }
                }
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
            public Action<StringBuilder> AppendStatsAction { get; set; }

            public string ComponentName { get; set; }

            public int Priority { get; set; }

            public StatsType StatsType { get; set; }
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
