using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Persistent implementation of the <see cref="ProvenBlockHeader"></see> DBreeze repository.
    /// </summary>
    public class ProvenBlockHeaderRepository : IProvenBlockHeaderRepository
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        /// <summary>Specification of the network the node runs on - RegTest/TestNet/MainNet.</summary>
        private readonly Network network;

        /// <summary>Database key under which the <see cref="ProvenBlockHeader"/> item is stored.</summary>
        private static readonly byte[] provenBlockHeaderKey = new byte[0];

        /// <summary>Database key under which the block hash of the <see cref="ProvenBlockHeader"/> tip is stored.</summary>
        private static readonly byte[] blockHashKey = new byte[0];

        /// <summary>DBreeze table names.</summary>
        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashTable = "BlockHash";

        /// <summary>Hash of the block which is currently the tip of the <see cref="ProvenBlockHeader"/>.</summary>
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
        public ProvenBlockHeaderRepository(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
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
        public ProvenBlockHeaderRepository(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            // Create the ProvenBlockHeaderStore if it doesn't exist.
            Directory.CreateDirectory(folder);

            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 400);
        }

        /// <inheritdoc />
        public Task InitializeAsync(uint256 blockHash = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogInformation("Initializing {0}.", nameof(ProvenBlockHeaderRepository));

                uint256 blockId = blockHash ?? this.network.GetGenesis().GetHash();

                using (DBreeze.Transactions.Transaction txn = this.dbreeze.GetTransaction())
                {
                    if (this.GetTipHash(txn) == null)
                    {
                        this.SetTipHash(txn, blockId);
                        txn.Commit();
                    }
                }

                this.logger.LogTrace("(-)");

            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task GetAsync(IEnumerable<StakeItem> stakeItems, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(stakeItems, nameof(stakeItems));
  
            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(stakeItems), stakeItems.Count());

                using (DBreeze.Transactions.Transaction txn = this.dbreeze.GetTransaction())
                {
                    txn.SynchronizeTables(ProvenBlockHeaderTable);

                    txn.ValuesLazyLoadingIsOn = false;

                    using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
                    {
                        foreach (StakeItem stakeItem in stakeItems)
                        {
                            this.logger.LogTrace("Loading ProvenBlockHeader hash '{0}' from the database.", stakeItem.BlockId);

                            Row<byte[], ProvenBlockHeader> row =
                                txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, stakeItem.BlockId.ToBytes(false));

                            if (row.Exists)
                            {
                                stakeItem.ProvenBlockHeader = row.Value;
                                stakeItem.InStore = true;
                            }
                        }
                    }

                    txn.ValuesLazyLoadingIsOn = true;

                    this.logger.LogTrace("(-)");
                }
            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(IEnumerable<StakeItem> stakeItems, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(stakeItems, nameof(stakeItems));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(stakeItems), stakeItems.Count());

                using (DBreeze.Transactions.Transaction txn = this.dbreeze.GetTransaction())
                {
                    txn.SynchronizeTables(BlockHashTable, ProvenBlockHeaderTable);

                    using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
                    {
                        this.InsertProvenHeaders(txn, stakeItems);

                        txn.Commit();
                    }
                }

                this.logger.LogTrace("(-)");

            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<uint256> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                uint256 tipHash;

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    tipHash = this.GetTipHash(transaction);

                }

                this.logger.LogTrace("(-):'{0}'", tipHash);

                return tipHash;

            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }

        /// <summary>
        /// Obtains a block hash of the current tip.
        /// </summary>
        /// <param name="txn">Open DBreeze transaction.</param>
        /// <returns>Hash of blocks current tip.</returns>
        private uint256 GetTipHash(DBreeze.Transactions.Transaction txn)
        {
            if (this.blockHash == null)
            {
                txn.ValuesLazyLoadingIsOn = false;

                Row<byte[], uint256> row = txn.Select<byte[], uint256>(BlockHashTable, blockHashKey);

                if (row.Exists)
                    this.blockHash = row.Value;

                txn.ValuesLazyLoadingIsOn = true;
            }

            return this.blockHash;
        }

        /// <summary>
        /// Set's the tip to a new block hash.  ### re word ###
        /// </summary>
        /// <param name="txn">Open DBreeze transaction.</param>
        /// <param name="blockId">Hash of the block to become the new tip.</param>
        private void SetTipHash(DBreeze.Transactions.Transaction txn, uint256 blockId)
        {
            Guard.NotNull(blockId, nameof(blockId));

            this.logger.LogTrace("({0}:'{1}')", nameof(blockId), blockId);

            this.blockHash = blockId;

            txn.Insert<byte[], uint256>(BlockHashTable, blockHashKey, blockId);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/>s from <see cref="StakeItem"/>s, and adds them to the database.
        /// </summary>
        /// <param name="txn">Open DBreeze transaction.</param>
        /// <param name="stakeItems">List of <see cref="StakeItem"/>s.</param>
        private void InsertProvenHeaders(DBreeze.Transactions.Transaction txn, IEnumerable<StakeItem> stakeItems)
        {
            this.logger.LogTrace("({0}.Count():{1})", nameof(stakeItems), stakeItems.Count());

            IEnumerable<StakeItem> sortedStakeItems = this.SortProvenHeaders(txn, stakeItems);

            foreach (StakeItem stakeItem in sortedStakeItems)
            {
                if (!stakeItem.InStore)
                {
                    txn.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, stakeItem.BlockId.ToBytes(false), stakeItem.ProvenBlockHeader);
                    stakeItem.InStore = true;
                }

                this.SetTipHash(txn, stakeItem.BlockId);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sorts <see cref="ProvenBlockHeader"/>s.
        /// </summary>
        /// <param name="txn">Open DBreeze transaction.</param>
        /// <param name="stakeItems">List of <see cref="StakeItem"/>s.</param>
        /// <returns><see cref="StakeItem"/> enumerator.</returns>
        private IEnumerable<StakeItem> SortProvenHeaders(DBreeze.Transactions.Transaction txn, IEnumerable<StakeItem> stakeItems)
        {
            var stakeDict = new Dictionary<uint256, StakeItem>();

            foreach(StakeItem item in stakeItems)
            {
                stakeDict[item.BlockId] = item;
            }

            List<KeyValuePair<uint256, StakeItem>> stakeItemList = stakeDict.ToList();

            stakeItemList.Sort((pair1, pair2) => pair1.Value.Height.CompareTo(pair2.Value.Height));

            txn.ValuesLazyLoadingIsOn = false;

            foreach (KeyValuePair<uint256, StakeItem> stakeItem in stakeItemList)
            {
                StakeItem outStakeItem = stakeItem.Value;

                // Check if the header already exists in the database.
                Row<byte[], ProvenBlockHeader> headerRow = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, outStakeItem.BlockId.ToBytes());

                if (!headerRow.Exists)
                {
                    yield return outStakeItem;
                }
            }

            txn.ValuesLazyLoadingIsOn = true;
        }

        /// <summary>
        /// Checks whether a <see cref="ProvenBlockHeader"/> exists in the database.
        /// </summary>
        /// <param name="txn">Open DBreeze transaction.</param>
        /// <param name="blockId">Block hash key to search on.</param>
        /// <returns>True if the items exists in the database.</returns>
        private bool ProvenBlockHeaderExists(DBreeze.Transactions.Transaction txn, uint256 blockId)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockId), blockId);

            txn.ValuesLazyLoadingIsOn = false;

            Row<byte[], ProvenBlockHeader> row = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockId.ToBytes());

            txn.ValuesLazyLoadingIsOn = true;
        
            this.logger.LogTrace("(-):{0}", row.Exists);

            return row.Exists;
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            this.logger.LogTrace("()");

            benchLog.AppendLine("======ProvenBlockHeaderRepository Bench======");

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
