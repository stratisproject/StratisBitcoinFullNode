using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepository : IDisposable
    {
        Task LoadAsync(ConcurrentChain chain);

        Task SaveAsync(ConcurrentChain chain);
    }

    public interface IFinalizedBlockHeight : IDisposable
    {
        /// <summary>Gets the finalized block height.</summary>
        /// <returns>Height of a block that can't be reorged away from.</returns>
        int GetFinalizedBlockHeight();

        /// <summary>Loads the finalized block height.</summary>
        /// <returns>Height of a block that can't be reorged away from.</returns>
        Task LoadFinalizedBlockHeightAsync();

        /// <summary>Saves the finalized block height.</summary>
        /// <param name="height">Block height.</param>
        Task SaveFinalizedBlockHeightAsync(int height);
    }

    public class ChainRepository : IChainRepository, IFinalizedBlockHeight
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        private BlockLocator locator;

        /// <summary>Database key under which the block hash of the coin view's current tip is stored.</summary>
        private static readonly byte[] finalizedBlockKey = new byte[0];

        /// <summary>Height of a block that can't be reorged away from.</summary>
        private int finalizedHeight;

        public ChainRepository(string folder, ILoggerFactory loggerFactory)
        {
            Guard.NotEmpty(folder, nameof(folder));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.dbreeze = new DBreezeEngine(folder);
        }

        public ChainRepository(DataFolder dataFolder, ILoggerFactory loggerFactory)
            : this(dataFolder.ChainPath, loggerFactory)
        {
        }

        /// <inheritdoc />
        public int GetFinalizedBlockHeight()
        {
            return this.finalizedHeight;
        }

        /// <inheritdoc />
        public Task LoadFinalizedBlockHeightAsync()
        {
            this.logger.LogTrace("()");

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    
                    Row<byte[], int> row = transaction.Select<byte[], int>("FinalizedBlock", finalizedBlockKey);
                    if (!row.Exists)
                    {
                        this.finalizedHeight = 0;
                        this.logger.LogTrace("(-):0");
                        return;
                    }
                    
                    this.finalizedHeight = row.Value;
                    this.logger.LogTrace("(-):{0}", row.Value);
                }
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task SaveFinalizedBlockHeightAsync(int height)
        {
            this.logger.LogTrace("({0}:{1})", nameof(height), height);

            this.finalizedHeight = height;

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.Insert<byte[], int>("FinalizedBlock", finalizedBlockKey, height);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        public Task LoadAsync(ConcurrentChain chain)
        {
            Guard.Assert(chain.Tip == chain.Genesis);

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    ChainedHeader tip = null;
                    Row<int, BlockHeader> firstRow = transaction.Select<int, BlockHeader>("Chain", 0);

                    if (!firstRow.Exists)
                        return;

                    BlockHeader previousHeader = firstRow.Value;
                    Guard.Assert(previousHeader.GetHash() == chain.Genesis.HashBlock); // can't swap networks

                    foreach (Row<int, BlockHeader> row in transaction.SelectForwardSkip<int, BlockHeader>("Chain", 1))
                    {
                        if ((tip != null) && (previousHeader.HashPrevBlock != tip.HashBlock))
                            break;

                        tip = new ChainedHeader(previousHeader, row.Value.HashPrevBlock, tip);
                        previousHeader = row.Value;
                    }

                    if (previousHeader != null)
                        tip = new ChainedHeader(previousHeader, previousHeader.GetHash(), tip);

                    if (tip == null)
                        return;

                    this.locator = tip.GetLocator();
                    chain.SetTip(tip);
                }
            });

            return task;
        }

        public Task SaveAsync(ConcurrentChain chain)
        {
            Guard.NotNull(chain, nameof(chain));

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    ChainedHeader fork = this.locator == null ? null : chain.FindFork(this.locator);
                    ChainedHeader tip = chain.Tip;
                    ChainedHeader toSave = tip;

                    var headers = new List<ChainedHeader>();
                    while (toSave != fork)
                    {
                        headers.Add(toSave);
                        toSave = toSave.Previous;
                    }

                    // DBreeze is faster on ordered insert.
                    IOrderedEnumerable<ChainedHeader> orderedChainedHeaders = headers.OrderBy(b => b.Height);
                    foreach (ChainedHeader block in orderedChainedHeaders)
                    {
                        transaction.Insert("Chain", block.Height, block.Header);
                    }

                    this.locator = tip.GetLocator();
                    transaction.Commit();
                }
            });

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }
    }
}
