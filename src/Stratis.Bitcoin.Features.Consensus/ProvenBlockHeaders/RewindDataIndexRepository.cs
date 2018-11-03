using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Persistent implementation of the <see cref="RewindData"/> DBreeze repository.
    /// </summary>
    public class RewindDataIndexRepository : IRewindDataIndexRepository
    {
        /// <summary>
        /// Instance logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Access to DBreeze database.
        /// </summary>
        private readonly DBreezeEngine dbreeze;

        /// <summary>
        /// DBreeze table names.
        /// </summary>
        private const string RewindDataIndexTable = "RewindDataIndex";

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="folder"><see cref="ProvenBlockHeaderRepository"/> folder path to the DBreeze database files.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        public RewindDataIndexRepository(string folder, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Directory.CreateDirectory(folder);

            this.dbreeze = new DBreezeEngine(folder);
        }

        /// <inheritdoc />
        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    // hydrate rewind data

                    transaction.Commit();
                }
            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task<int?> GetAsync(string key, CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<int?> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(RewindDataIndexTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    this.logger.LogTrace("Trying to find rewind data item record with a key {key} in the database.", key);
                    Row<string, int> row = transaction.Select<string, int>(RewindDataIndexTable, key);

                    if (row.Exists)
                    {
                        return row.Value;
                    }

                    return (int?)null;
                }
            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(string key, int rewindDataIndex, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(key, nameof(key));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("Store item ({key}:{value})", key, rewindDataIndex);

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(RewindDataIndexTable);

                    transaction.Insert<string, int>(RewindDataIndexTable, key, rewindDataIndex);

                    transaction.Commit();
                }
            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(IDictionary<string, int> items, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(items, nameof(items));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("Storing rewind data index ({items}.Count():{count})", nameof(items), items.Count);

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(RewindDataIndexTable);

                    foreach (KeyValuePair<string, int> item in items)
                    {
                        this.logger.LogTrace("Store item ({key}:{value})", item.Key, item.Value);

                        transaction.Insert<string, int>(RewindDataIndexTable, item.Key, item.Value);
                    }

                    transaction.Commit();
                }
            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }
    }
}
