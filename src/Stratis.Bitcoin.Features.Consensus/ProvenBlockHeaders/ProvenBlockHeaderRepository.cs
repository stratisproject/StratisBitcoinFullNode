﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Persistent implementation of the <see cref="ProvenBlockHeader"/> DBreeze repository.
    /// </summary>
    public class ProvenBlockHeaderRepository : IProvenBlockHeaderRepository
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
        /// Specification of the network the node runs on - RegTest/TestNet/MainNet.
        /// </summary>
        private readonly Network network;

        /// <summary>
        /// Database key under which the block hash and height of a <see cref="ProvenBlockHeader"/> tip is stored.
        /// </summary>
        private static readonly byte[] blockHashHeightKey = new byte[0];

        /// <summary>
        /// DBreeze table names.
        /// </summary>
        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashHeightTable = "BlockHashHeight";

        /// <summary>
        /// Current <see cref="ProvenBlockHeader"/> tip.
        /// </summary>
        private ProvenBlockHeader provenBlockHeaderTip;

        /// <inheritdoc />
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderRepository"/> folder path to the DBreeze database files.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        public ProvenBlockHeaderRepository(Network network, string folder, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Directory.CreateDirectory(folder);

            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
        }

        /// <inheritdoc />
        public Task InitializeAsync()
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    this.TipHashHeight = this.GetTipHash(transaction);

                    if (this.TipHashHeight != null)
                        return;

                    var hashHeight = new HashHeightPair(this.network.GetGenesis().GetHash(), 0);

                    this.SetTip(transaction, hashHeight);

                    transaction.Commit();

                    this.TipHashHeight = hashHeight;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight)
        {
            Task<List<ProvenBlockHeader>> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(ProvenBlockHeaderTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    this.logger.LogTrace("Loading ProvenBlockHeaders from block height {0} to {1} from the database.",
                        fromBlockHeight, toBlockHeight);

                    var headers = new List<ProvenBlockHeader>();

                    for (int i = fromBlockHeight; i <= toBlockHeight; i++)
                    {
                        Row<byte[], ProvenBlockHeader> row =
                            transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, i.ToBytes(false));

                        if (row.Exists)
                        {
                            headers.Add(row.Value);
                        }
                        else
                        {
                            this.logger.LogDebug("ProvenBlockHeader height {0} does not exist in the database.", i);
                            headers.Add(null);
                        }
                    }

                    return headers;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            IEnumerable<ProvenBlockHeader> headers =  await this.GetAsync(blockHeight, blockHeight).ConfigureAwait(false);

            return headers.FirstOrDefault();
        }

        /// <inheritdoc />
        public Task PutAsync(List<ProvenBlockHeader> headers, HashHeightPair newTip)
        {
            Guard.NotNull(headers, nameof(headers));
            Guard.NotNull(newTip, nameof(newTip));

            Guard.Assert(newTip.Hash == headers.Last().GetHash());

            if ((this.provenBlockHeaderTip != null) && (newTip.Hash == this.provenBlockHeaderTip.GetHash()))
            {
                this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");

                throw new ProvenBlockHeaderException("Invalid new tip hash, tip hash has not changed.");
            }

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(headers), headers.Count());

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockHashHeightTable, ProvenBlockHeaderTable);

                    this.InsertHeaders(transaction, headers);

                    this.SetTip(transaction, newTip);

                    transaction.Commit();

                    this.TipHashHeight = newTip;
                }
            });

            return task;
        }

        /// <summary>
        /// Set's the hash and height tip of the new <see cref="ProvenBlockHeader"/>.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <param name="newTip"> Hash height pair of the new block tip.</param>
        private void SetTip(DBreeze.Transactions.Transaction transaction, HashHeightPair newTip)
        {
            Guard.NotNull(newTip, nameof(newTip));

            transaction.Insert<byte[], HashHeightPair>(BlockHashHeightTable, blockHashHeightKey, newTip);
        }

        /// <summary>
        /// Inserts <see cref="ProvenBlockHeader"/> items into to the database.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <param name="headers"> List of <see cref="ProvenBlockHeader"/> items to save.</param>
        private void InsertHeaders(DBreeze.Transactions.Transaction transaction, List<ProvenBlockHeader> headers)
        {
            var headerDict = new Dictionary<int, ProvenBlockHeader>();

            // Gather headers.
            for (int i = headers.Count - 1; i > -1; i--)
                headerDict[i] = headers[i];

            List<KeyValuePair<int, ProvenBlockHeader>> sortedHeaders = headerDict.ToList();

            sortedHeaders.Sort((pair1, pair2) => pair1.Key.CompareTo(pair2.Key));

            foreach (KeyValuePair<int, ProvenBlockHeader> header in sortedHeaders)
                transaction.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, header.Key.ToBytes(false), header.Value);

            // Store the latest ProvenBlockHeader in memory.
            this.provenBlockHeaderTip = sortedHeaders.Last().Value;
        }

        /// <summary>
        /// Retrieves the current <see cref="HashHeightPair"/> tip from disk.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <returns> Hash of blocks current tip.</returns>
        private HashHeightPair GetTipHash(DBreeze.Transactions.Transaction transaction)
        {
            HashHeightPair tipHash = null;

            Row<byte[], HashHeightPair> row = transaction.Select<byte[], HashHeightPair>(BlockHashHeightTable, blockHashHeightKey);

            if (row.Exists)
                tipHash = row.Value;

            return tipHash;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }
    }
}
