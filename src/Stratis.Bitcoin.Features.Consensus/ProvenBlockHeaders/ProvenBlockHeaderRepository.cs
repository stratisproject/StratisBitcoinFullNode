using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
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

        /// <summary>Database key under which the block hash and height of a <see cref="ProvenBlockHeader"/> tip is stored.</summary>
        private static readonly byte[] blockHashHeightKey = new byte[0];

        /// <summary>DBreeze table names.</summary>
        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashHeightTable = "BlockHashHeight";

        /// <summary>Height of the block which is currently the tip of the <see cref="ProvenBlockHeader"/>.</summary>
        private HashHeightPair blockHashHeightPair;

        /// <summary>Current <see cref="ProvenBlockHeader"/> tip.</summary>
        private ProvenBlockHeader provenBlockHeaderTip;
        
        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderStore"/> folder path to the DBreeze database files.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        public ProvenBlockHeaderRepository(Network network, string folder, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            // Create the ProvenBlockHeaderStore if it doesn't exist.
            Directory.CreateDirectory(folder);

            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
        }

        /// <inheritdoc />
        public Task InitializeAsync()
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogInformation("Initializing {0}.", nameof(ProvenBlockHeaderRepository));

                Block genesis = this.network.GetGenesis();

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    if (this.GetTipHashHeight(transaction) == null)
                    {
                        // set to genesis
                        this.SetTip(transaction, new HashHeightPair(genesis.GetHash(), 0));

                        transaction.Commit();
                    }
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        public Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight)
        {
            Task<List<ProvenBlockHeader>> task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}:'{1}')", nameof(fromBlockHeight), fromBlockHeight);
                this.logger.LogTrace("({0}:'{1}')", nameof(toBlockHeight), toBlockHeight);

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    List<ProvenBlockHeader> items = new List<ProvenBlockHeader>();

                    transaction.SynchronizeTables(ProvenBlockHeaderTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    this.logger.LogTrace("Loading ProvenBlockHeaders from block height '{0}' to '{1}  from the database.",
                        fromBlockHeight, toBlockHeight);

                    for (int i = fromBlockHeight; i <= toBlockHeight; i++)
                    {
                        Row<byte[], ProvenBlockHeader> row =
                            transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, i.ToBytes(false));

                        if (row.Exists)
                            items.Add(row.Value);
                    }

                    this.logger.LogTrace("(-)");

                    return items;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            Guard.NotNull(blockHeight, nameof(blockHeight));

            Task<ProvenBlockHeader> task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}:'{1}')", nameof(blockHeight), blockHeight);

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    ProvenBlockHeader header = null;

                    transaction.SynchronizeTables(ProvenBlockHeaderTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    this.logger.LogTrace("Loading ProvenBlockHeader hash '{0}' from the database.", blockHeight);

                    Row<byte[], ProvenBlockHeader> row =
                        transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHeight.ToBytes(false));

                    if (row.Exists)
                        header = row.Value;

                    this.logger.LogTrace("(-)");

                    return header;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(List<ProvenBlockHeader> headers, HashHeightPair newTip)  
        {
            Guard.NotNull(headers, nameof(headers));
            Guard.NotNull(newTip, nameof(newTip));            

            if ((this.provenBlockHeaderTip != null) && (newTip.Hash != this.provenBlockHeaderTip.HashPrevBlock))
            {
                this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                throw new InvalidOperationException("Invalid newTip block hash.");
            }

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(headers), headers.Count());

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockHashHeightTable, ProvenBlockHeaderTable);

                    this.InsertHeaders(transaction, headers, newTip);
                    this.SetTip(transaction, newTip);

                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task<HashHeightPair> GetTipHashHeightAsync()
        {
            Task<HashHeightPair> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                HashHeightPair tip;

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    tip = this.GetTipHashHeight(transaction);
                }

                this.logger.LogTrace("(-):'{0}'", tip);

                return tip;
            });

            return task;
        }

        /// <summary>
        /// Delete <see cref="ProvenBlockHeader"/> items.
        /// </summary>
        /// <param name="fromBlockHeight">Block height to start range.</param>
        /// <param name="toBlockHeight">Block height end range.</param>
        private Task DeleteAsync(int fromBlockHeight, int toBlockHeight)
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}:'{1}')", nameof(fromBlockHeight), fromBlockHeight);
                this.logger.LogTrace("({0}:'{1}')", nameof(toBlockHeight), toBlockHeight);

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockHashHeightTable, ProvenBlockHeaderTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    for (int i = fromBlockHeight; i <= toBlockHeight; i++)
                        transaction.RemoveKey<byte[]>(ProvenBlockHeaderTable, i.ToBytes(false));

                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");

            return task;
        }

        /// <summary>
        /// Obtains a block hash and height of the current tip.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <returns><see cref="HashHeightPair"/> of current <see cref="ProvenBlockHeader"/> tip.</returns>
        private HashHeightPair GetTipHashHeight(DBreeze.Transactions.Transaction transaction)
        {
            HashHeightPair tip = this.blockHashHeightPair;

            if (tip == null)
            {
                transaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], HashHeightPair> row = transaction.Select<byte[], HashHeightPair>(BlockHashHeightTable, blockHashHeightKey);

                if (row.Exists)
                    this.blockHashHeightPair = row.Value;
            }

            return this.blockHashHeightPair;
        }

        /// <summary>
        /// Set's the hash and height tip of the new <see cref="ProvenBlockHeader"/>.  
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="newTip">Hash height pair of the new block tip.</param>
        private void SetTip(DBreeze.Transactions.Transaction transaction, HashHeightPair newTip)
        {
            Guard.NotNull(newTip, nameof(newTip));

            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            this.blockHashHeightPair = newTip;

            transaction.Insert<byte[], HashHeightPair>(BlockHashHeightTable, blockHashHeightKey, newTip);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Inserts <see cref="ProvenBlockHeader"/> items into to the database.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="headers">List of <see cref="ProvenBlockHeader"/> items to save.</param>
        /// <param name="newTip">Hash and height of the new repository's tip.</param>
        private void InsertHeaders(DBreeze.Transactions.Transaction transaction, List<ProvenBlockHeader> headers, HashHeightPair newTip)
        {
            this.logger.LogTrace("({0}.Count():{1})", nameof(headers), headers.Count());

            int tipHeight = newTip.Height;

            var headerDict = new Dictionary<int, ProvenBlockHeader>();

            // Gather headers.
            foreach (ProvenBlockHeader header in headers)
            {
                headerDict[tipHeight] = header;
                tipHeight--;
            }

            var sortedHeaders = headerDict.ToList();
            sortedHeaders.Sort((pair1, pair2) => pair1.Key.CompareTo(pair2.Key));

            foreach (KeyValuePair<int, ProvenBlockHeader> header in sortedHeaders)
            {
                transaction.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, header.Key.ToBytes(false), header.Value);
            }

            // Store the latest ProvenBlockHeader in memory.
            this.provenBlockHeaderTip = headerDict.Values.LastOrDefault();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks whether a <see cref="ProvenBlockHeader"/> exists in the database.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="blockHeight">Block height key to search on.</param>
        /// <returns>True if the items exists in the database.</returns>
        private bool ProvenBlockHeaderExists(DBreeze.Transactions.Transaction transaction, int blockHeight)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHeight), blockHeight);

            Row<byte[], ProvenBlockHeader> row = transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHeight.ToBytes(false));
        
            this.logger.LogTrace("(-):{0}", row.Exists);

            return row.Exists;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }
    }
}
