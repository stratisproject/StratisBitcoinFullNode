using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStore : IProvenBlockHeaderStore, IDisposable
    {
        /// <summary>Database key under which the block hash of the ChainedHeader's current tip is stored.</summary>
        private static readonly byte[] blockHashKey = new byte[0];

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        /// <summary>Specification of the network the node runs on - RegTest/TestNet/MainNet.</summary>
        private readonly Network network;

        /// <summary>The highest stored block in the repository.</summary>
        private ChainedHeader storeTip;

        private readonly int threshold;

        private readonly int thresholdWindow;

        private readonly ConcurrentDictionary<uint256, ProvenBlockHeader> items = 
            new ConcurrentDictionary<uint256, ProvenBlockHeader>();   

        /// <summary>Hash of the block which is currently the tip of the ChainedHeader.</summary>
        private uint256 blockHash;

        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        private readonly BackendPerformanceCounter performanceCounter;
        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="dataFolder">Information about path locations to important folders and files on disk.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        public ProvenBlockHeaderStore(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
            : this(network, dataFolder.ProvenBlockHeaderPath, dateTimeProvider, loggerFactory, nodeStats)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderStore"/> folder path to the DBreeze database files.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        public ProvenBlockHeaderStore(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));

            // Create the ProvenBlockHeaderStore if it doesn't exist.
            Directory.CreateDirectory(folder);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 400);
        }

        /// <summary>
        /// Initializes the database table used by the <see cref="ProvenBlockHeader"/>.
        /// </summary>
        public Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    transaction.SynchronizeTables("ProvenBlockHeader");

                    Block genesis = this.network.GetGenesis();

                    var load = new List<ProvenBlockHeader>();

                    //TODO: Need to understand what to do in here as different stores do different things
                    // see BlockRepository.InitializeAsync, BlockStoreQueue.InitializeAsync


                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Pop some items to remove a window of 10 % of the threshold.
            throw new NotImplementedException();
        }

        public Task<ProvenBlockHeader> GetAsync(uint256 blockId, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<ProvenBlockHeader> GetTipAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task SetAsync(ProvenBlockHeader provenBlockHeader, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze.Dispose();
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            this.logger.LogTrace("()");

            benchLog.AppendLine("======ProvenBlockHeaderStore Bench======");

            BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                benchLog.AppendLine(snapShot.ToString());
            else
                benchLog.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;

            this.logger.LogTrace("(-)");
        }
    }
}
