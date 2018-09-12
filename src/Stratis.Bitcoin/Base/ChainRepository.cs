using System;
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

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepository : IDisposable
    {
        /// <summary>Loads the chain of headers from the database.</summary>
        /// <returns>Tip of the loaded chain.</returns>
        Task<ChainedHeader> LoadAsync(ChainedHeader genesisHeader);

        /// <summary>Persists chain of headers to the database.</summary>
        Task SaveAsync(ConcurrentChain chain);
    }

    /// <summary>Provider of the last finalized block's height and hash.</summary>
    /// <remarks>
    /// Finalized block height is the height of the last block that can't be reorged.
    /// Blocks with height greater than finalized height can be reorged.
    /// <para>Finalized block height value is always <c>0</c> for blockchains without max reorg property.</para>
    /// </remarks>
    public interface IFinalizedBlockInfo
    {
        /// <summary>Gets the finalized block hash and height.</summary>
        /// <returns>Hash and height of a block that can't be reorged away from.</returns>
        HashHeightPair GetFinalizedBlockInfo();

        /// <summary>Loads the finalised block hash and height from the database.</summary>
        Task LoadFinalizedBlockInfoAsync(Network network);

        /// <summary>Saves the finalized block hash and height to the database if height is greater than the previous value.</summary>
        /// <param name="hash">Block hash.</param>
        /// <param name="height">Block height.</param>
        /// <returns><c>true</c> if new value was set, <c>false</c> if <paramref name="height"/> is lower or equal than current value.</returns>
        Task<bool> SaveFinalizedBlockHashAndHeightAsync(uint256 hash, int height);
    }

    public class ChainRepository : IChainRepository, IFinalizedBlockInfo
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        private BlockLocator locator;

        /// <summary>Database key under which the block height of the last finalized block height is stored.</summary>
        private static readonly byte[] finalizedBlockKey = new byte[0];

        /// <summary>Height and hash of a block that can't be reorged away from.</summary>
        private HashHeightPair finalizedBlockInfo;

        public ChainRepository(string folder, ILoggerFactory loggerFactory)
        {
            Guard.NotEmpty(folder, nameof(folder));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Directory.CreateDirectory(folder);
            this.dbreeze = new DBreezeEngine(folder);
        }

        public ChainRepository(DataFolder dataFolder, ILoggerFactory loggerFactory)
            : this(dataFolder.ChainPath, loggerFactory)
        {
        }

        /// <inheritdoc />
        public HashHeightPair GetFinalizedBlockInfo()
        {
            return this.finalizedBlockInfo;
        }

        /// <inheritdoc />
        public Task LoadFinalizedBlockInfoAsync(Network network)
        {
            this.logger.LogTrace("()");

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], HashHeightPair> row = transaction.Select<byte[], HashHeightPair>("FinalizedBlock", finalizedBlockKey);
                    if (!row.Exists)
                    {
                        this.finalizedBlockInfo = new HashHeightPair(network.GenesisHash, 0);
                        this.logger.LogTrace("Finalized block height doesn't exist in the database.");
                    }
                    else
                        this.finalizedBlockInfo = row.Value;

                    this.logger.LogTrace("(-):{0}='{1}'", nameof(this.finalizedBlockInfo), this.finalizedBlockInfo);
                }
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<bool> SaveFinalizedBlockHashAndHeightAsync(uint256 hash, int height)
        {
            this.logger.LogTrace("({0}:{1})", nameof(height), height);

            if (this.finalizedBlockInfo != null && height <= this.finalizedBlockInfo.Height)
            {
                this.logger.LogTrace("(-)[CANT_GO_BACK]:false");
                return Task.FromResult(false);
            }

            this.finalizedBlockInfo = new HashHeightPair(hash, height);

            Task<bool> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.Insert<byte[], HashHeightPair>("FinalizedBlock", finalizedBlockKey, this.finalizedBlockInfo);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-):true");
                return true;
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<ChainedHeader> LoadAsync(ChainedHeader genesisHeader)
        {
            Task<ChainedHeader> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    ChainedHeader tip = null;
                    Row<int, BlockHeader> firstRow = transaction.Select<int, BlockHeader>("Chain", 0);

                    if (!firstRow.Exists)
                        return genesisHeader;

                    BlockHeader previousHeader = firstRow.Value;
                    Guard.Assert(previousHeader.GetHash() == genesisHeader.HashBlock); // can't swap networks

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
                        tip = genesisHeader;

                    this.locator = tip.GetLocator();
                    return tip;
                }
            });

            return task;
        }

        /// <inheritdoc />
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
