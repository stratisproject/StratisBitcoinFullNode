using System;
using System.IO;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary></summary>
    public interface ITemplateTransactionStore : IDisposable
    {
        /// <summary>
        /// Persist the template transaction into the database.
        /// </summary>
        /// <param name="templateTransaction">Template transaction to be inserted.</param>
        Task PutAsync(TemplateTransaction templateTransaction);

        /// <summary>
        /// Get the template transaction from the database, identified by a hash.
        /// </summary>
        /// <param name="hash">A template transaction hash.</param>
        /// <returns>The template transaction identified by the hash (or null if not found).</returns>
        Task<TemplateTransaction> GetAsync(uint256 hash);

        /// <summary>
        /// Delete a template transaction from the database, identified by a hash.
        /// </summary>
        /// <param name="hash">Hash of the template transaction to be deleted.</param>
        Task DeleteAsync(uint256 hash);

        /// <summary>
        /// Determine if a template transaction already exists in the database.
        /// </summary>
        /// <param name="hash">The hash of the template transaction.</param>
        /// <returns><c>true</c> if the template transaction can be found in the database, otherwise return <c>false</c>.</returns>
        Task<bool> ExistAsync(uint256 hash);
    }

    public class TemplateTransactionStore : ITemplateTransactionStore
    {
        private const string TemplateTransactionTableName = "TemplateTransaction";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;

        private readonly Network network;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public TemplateTransactionStore(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(network, Path.Combine(dataFolder.RootPath, "sidechaindata"), dateTimeProvider, loggerFactory)
        {
        }

        public TemplateTransactionStore(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);
            this.network = network;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>Performs any needed initialisation for the database.</summary>
        public virtual Task InitializeAsync()
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                // Currently don't do anything on startup

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task<TemplateTransaction> GetAsync(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            Task<TemplateTransaction> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                TemplateTransaction res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], TemplateTransaction> templateRow = transaction.Select<byte[], TemplateTransaction>(TemplateTransactionTableName, hash.ToBytes());

                    if (templateRow.Exists)
                    {
                        res = templateRow.Value;
                    }
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(TemplateTransaction templateTransaction)
        {
            Guard.NotNull(templateTransaction, nameof(templateTransaction));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    this.OnInsertTemplateTransaction(transaction, templateTransaction);

                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task<bool> ExistAsync(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            Task<bool> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                bool res = false;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    // Lazy loading is on so we don't fetch the whole value, just the row.
                    Row<byte[], TemplateTransaction> templateRow = transaction.Select<byte[], TemplateTransaction>(TemplateTransactionTableName, hash.ToBytes());

                    if (templateRow.Exists)
                    {
                        res = true;
                    }
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            return task;
        }

        /// <inheritdoc />
        public Task DeleteAsync(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    this.OnDeleteTemplateTransaction(transaction, hash);

                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        protected virtual void OnInsertTemplateTransaction(DBreeze.Transactions.Transaction dbreezeTransaction, TemplateTransaction templateTransaction)
        {
            // If the template is already in store don't write it again.
            Row<byte[], TemplateTransaction> templateRow = dbreezeTransaction.Select<byte[], TemplateTransaction>(TemplateTransactionTableName, templateTransaction.Hash.ToBytes());

            if (!templateRow.Exists)
            {
                dbreezeTransaction.Insert<byte[], TemplateTransaction>(TemplateTransactionTableName, templateTransaction.Hash.ToBytes(), templateTransaction);
            }
        }

        protected virtual void OnDeleteTemplateTransaction(DBreeze.Transactions.Transaction dbreezeTransaction, uint256 hash)
        {
            dbreezeTransaction.RemoveKey<byte[]>(TemplateTransactionTableName, hash.ToBytes());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.DBreeze.Dispose();
        }
    }
}
