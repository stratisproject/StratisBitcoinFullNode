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

    /// <summary>Provider of the last finalized block height.</summary>
    /// <remarks>
    /// Finalized block height is the height of the last block that can't be reorged.
    /// Blocks with height greater than finalized height can be reorged.
    /// <para>Finalized block height value is always <c>0</c> for blockchains without max reorg property.</para>
    /// </remarks>
    public interface IFinalizedBlockHeight
    {
        /// <summary>Gets the finalized block height.</summary>
        /// <returns>Height of a block that can't be reorged away from.</returns>
        int GetFinalizedBlockHeight();

        /// <summary>Loads the finalised block height from the database.</summary>
        Task LoadFinalizedBlockHeightAsync();

        /// <summary>Saves the finalized block height to the database if it is greater than the previous value.</summary>
        /// <param name="height">Block height.</param>
        /// <returns><c>true</c> if new value was set, <c>false</c> if <paramref name="height"/> is lower or equal than current value.</returns>
        Task<bool> SaveFinalizedBlockHeightAsync(int height);
    }

    public class ChainRepository : IChainRepository, IFinalizedBlockHeight
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        private BlockLocator locator;

        /// <summary>Database key under which the block height of the last finalized block height is stored.</summary>
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
                        this.logger.LogTrace("Finalized block height doesn't exist in the database.");
                    }
                    else
                        this.finalizedHeight = row.Value;
                    
                    this.logger.LogTrace("(-):{0}={1}", nameof(this.finalizedHeight), this.finalizedHeight);
                }
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<bool> SaveFinalizedBlockHeightAsync(int height)
        {
            this.logger.LogTrace("({0}:{1})", nameof(height), height);

            if (height <= this.finalizedHeight)
            {
                this.logger.LogTrace("(-)[CANT_GO_BACK]:false");
                return Task.FromResult(false);
            }
            
            this.finalizedHeight = height;

            Task<bool> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.Insert<byte[], int>("FinalizedBlock", finalizedBlockKey, height);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-):true");
                return true;
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
