using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Persistent implementation of the <see cref="RewindData"/> DBreeze repository.
    /// </summary>
    public class RewindDataIndexRepository : IRewindDataIndexRepository
    {
        /// <summary>
        /// The DBreeze coin view instance for accessing DBreeze transaction factory.
        /// </summary>
        private readonly DBreezeCoinView dBreezeCoinView;

        /// <summary>
        /// Instance logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// DBreeze table names.
        /// </summary>
        private const string RewindDataIndexTable = "RewindDataIndex";

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="dBreezeCoinView"><see cref="DBreezeCoinView"/>.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        public RewindDataIndexRepository(DBreezeCoinView dBreezeCoinView, ILoggerFactory loggerFactory)
        {
            this.dBreezeCoinView = dBreezeCoinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public Task<int?> GetAsync(string key, CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<int?> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dBreezeCoinView.CreateTransaction())
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
        public Task PutAsync(IDictionary<string, int> items, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(items, nameof(items));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("Storing rewind data index ({items}.Count():{count})", nameof(items), items.Count);

                using (DBreeze.Transactions.Transaction transaction = this.dBreezeCoinView.CreateTransaction())
                {
                    transaction.SynchronizeTables(RewindDataIndexTable);

                    foreach (KeyValuePair<string, int> item in items.OrderBy(i => i.Key))
                    {
                        this.logger.LogTrace("Store item ({key}:{value})", item.Key, item.Value);

                        transaction.Insert<string, int>(RewindDataIndexTable, item.Key, item.Value);
                    }

                    transaction.Commit();
                }
            }, cancellationToken);

            return task;
        }
    }
}
