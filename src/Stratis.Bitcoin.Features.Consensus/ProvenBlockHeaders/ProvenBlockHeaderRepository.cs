using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
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

        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashTable = "BlockHash";


        /// <summary>Hash of the block which is currently the tip of the <see cref="ProvenBlockHeader"/>.</summary>
        private uint256 blockHash;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="dataFolder">Information about path locations to important folders and files on disk.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        public ProvenBlockHeaderRepository(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(network, dataFolder.ProvenBlockHeaderPath, dateTimeProvider, loggerFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderStore"/> folder path to the DBreeze database files.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        public ProvenBlockHeaderRepository(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            // Create the ProvenBlockHeaderStore if it doesn't exist.
            Directory.CreateDirectory(folder);

            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
        }

        public Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction txn = this.dbreeze.GetTransaction())
                {
                    txn.ValuesLazyLoadingIsOn = false;
                    txn.SynchronizeTables(BlockHashTable);

                    if (GetTipHash(txn) == null)
                    {
                        SetTipHash(txn, this.network.GetGenesis().GetHash());
                        txn.Commit();
                    }
                }

                /// TODO :
                /// neeed to init the ProvenBlockHeader table as well
                /// for stratis init from certain height - need to find  out
                /// Need to find out what we do other networks - for bitcoin ignore.

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");

            return task;
        }

        /// <inheritdoc />
        public Task GetAsync(IEnumerable<StakeItem> stakeItems)
        {
            Guard.NotNull(stakeItems, nameof(stakeItems));

            this.logger.LogTrace("({0}:'{1}')", nameof(stakeItems), stakeItems);

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(stakeItems), stakeItems.Count());

                using (DBreeze.Transactions.Transaction txn = this.dbreeze.GetTransaction())
                {
                    txn.SynchronizeTables(ProvenBlockHeaderTable);
                    txn.ValuesLazyLoadingIsOn = false;

                    foreach (StakeItem stakeItem in stakeItems)
                    {
                        this.logger.LogTrace("Loading ProvenBlockHeader hash '{0}' from the database.", stakeItem.BlockId);

                        Row<byte[], ProvenBlockHeader> row =
                            txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, stakeItem.BlockId.ToBytes(false));

                        if (row.Exists)
                        {
                            stakeItem.ProvenBlockHeader  = row.Value;
                            stakeItem.InStore = true;
                        }
                    }

                    this.logger.LogTrace("(-)");
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(IEnumerable<StakeItem> stakeItems)
        {
            Guard.NotNull(stakeItems, nameof(stakeItems));
            this.logger.LogTrace("({0}:'{1}')", nameof(stakeItems), stakeItems);

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(stakeItems), stakeItems.Count());

                using (DBreeze.Transactions.Transaction txn = this.dbreeze.GetTransaction())
                {
                    txn.SynchronizeTables(ProvenBlockHeaderTable);

                    foreach (StakeItem stakeItem in stakeItems)
                    {
                        if (!stakeItem.InStore)
                        {
                            txn.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, stakeItem.BlockId.ToBytes(), stakeItem.ProvenBlockHeader);
                            stakeItem.InStore = true;
                        }
                    }

                    txn.Commit();
                }

                this.logger.LogTrace("(-)");
            });

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
        /// <returns>Hash of block current tip.</returns>
        private uint256 GetTipHash(DBreeze.Transactions.Transaction txn)
        {
            if (this.blockHash == null)
            {
                Row<byte[], uint256> row = txn.Select<byte[], uint256>(BlockHashTable, blockHashKey);

                if (row.Exists)
                    this.blockHash = row.Value;
            }

            return this.blockHash;
        }

        /// <summary>
        /// Set's the tip to a new block hash.  ### re word ###
        /// </summary>
        /// <param name="txn">Open DBreeze transaction.</param>
        /// <param name="hash">Hash of the block to become the new tip.</param>
        private void SetTipHash(DBreeze.Transactions.Transaction txn, uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            this.logger.LogTrace("({0}:'{1}')", nameof(hash), hash);

            this.blockHash = hash;
            txn.Insert<byte[], uint256>(BlockHashTable, blockHashKey, hash);

            this.logger.LogTrace("(-)");
        }
    }
}
