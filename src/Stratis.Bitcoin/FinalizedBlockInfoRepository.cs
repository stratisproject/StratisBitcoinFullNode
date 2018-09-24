﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin
{
    /// <summary>Provider of the last finalized block's height and hash.</summary>
    /// <remarks>
    /// Finalized block height is the height of the last block that can't be reorged.
    /// Blocks with height greater than finalized height can be reorged.
    /// <para>Finalized block height value is always <c>0</c> for blockchains without max reorg property.</para>
    /// </remarks>
    public interface IFinalizedBlockInfoRepository : IDisposable
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
        bool SaveFinalizedBlockHashAndHeight(uint256 hash, int height);
    }

    public class FinalizedBlockInfoRepository : IFinalizedBlockInfoRepository
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        /// <summary>Database key under which the block height of the last finalized block height is stored.</summary>
        private static readonly byte[] finalizedBlockKey = new byte[0];

        /// <summary>Height and hash of a block that can't be reorged away from.</summary>
        private HashHeightPair finalizedBlockInfo;

        /// <summary>Queue of finalized infos to save.</summary>
        /// <remarks>All access should be protected by <see cref="queueLock"/></remarks>
        private readonly Queue<HashHeightPair> finalizedBlockInfosToSave;

        /// <summary>Protects access to <see cref="finalizedBlockInfosToSave"/>.</summary>
        private readonly object queueLock;

        /// <summary>Task that continously persists finalized block info to the database.</summary>
        private readonly Task finalizedBlockInfoPersistingTask;

        private readonly CancellationTokenSource cancellation;

        private readonly AsyncManualResetEvent queueUpdatedEvent;

        public FinalizedBlockInfoRepository(string folder, ILoggerFactory loggerFactory)
        {
            Guard.NotEmpty(folder, nameof(folder));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Directory.CreateDirectory(folder);
            this.dbreeze = new DBreezeEngine(folder);

            this.finalizedBlockInfosToSave = new Queue<HashHeightPair>();
            this.queueLock = new object();

            this.queueUpdatedEvent = new AsyncManualResetEvent(false);
            this.cancellation = new CancellationTokenSource();
            this.finalizedBlockInfoPersistingTask = this.PersistFinalizedBlockInfoContinuouslyAsync();
        }

        public FinalizedBlockInfoRepository(DataFolder dataFolder, ILoggerFactory loggerFactory)
            : this(dataFolder.FinalizedBlockInfoPath, loggerFactory)
        {
        }

        private async Task PersistFinalizedBlockInfoContinuouslyAsync()
        {
            this.logger.LogTrace("()");

            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    await this.queueUpdatedEvent.WaitAsync(this.cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }

                this.queueUpdatedEvent.Reset();

                HashHeightPair lastFinalizedBlock = null;

                lock (this.queueLock)
                {
                    // In case there are more than 1 items we take only the very last one.
                    // There is no need to save each of them because they are sequential and
                    // each new item has greater height than previous one.
                    while (this.finalizedBlockInfosToSave.Count != 0)
                        lastFinalizedBlock = this.finalizedBlockInfosToSave.Dequeue();
                }

                if (lastFinalizedBlock == null)
                    continue;

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.Insert<byte[], HashHeightPair>("FinalizedBlock", finalizedBlockKey, lastFinalizedBlock);
                    transaction.Commit();
                }

                this.logger.LogTrace("Finalized info saved: '{0}'.", lastFinalizedBlock);
            }

            this.logger.LogTrace("(-)");
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
        public bool SaveFinalizedBlockHashAndHeight(uint256 hash, int height)
        {
            this.logger.LogTrace("({0}:{1})", nameof(height), height);

            if (this.finalizedBlockInfo != null && height <= this.finalizedBlockInfo.Height)
            {
                this.logger.LogTrace("(-)[CANT_GO_BACK]:false");
                return false;
            }

            // Creating a new variable instead of assigning new value right away
            // to this.finalizedBlockInfo is needed because before we enqueue it
            // this.finalizedBlockInfo might change due to race condition.
            var finalizedInfo = new HashHeightPair(hash, height);

            this.finalizedBlockInfo = finalizedInfo;

            lock (this.queueLock)
            {
                this.finalizedBlockInfosToSave.Enqueue(finalizedInfo);
                this.queueUpdatedEvent.Set();
            }

            this.logger.LogTrace("(-):true");
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.cancellation.Cancel();
            this.finalizedBlockInfoPersistingTask.GetAwaiter().GetResult();

            this.dbreeze?.Dispose();

            this.logger.LogTrace("(-)");
        }
    }
}
