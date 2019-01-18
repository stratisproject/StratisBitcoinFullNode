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
using Stratis.Bitcoin.Interfaces;
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

        private readonly DBreezeSerializer dBreezeSerializer;

        /// <inheritdoc />
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderRepository"/> folder path to the DBreeze database files.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="dBreezeSerializer">The serializer to use for <see cref="IBitcoinSerializable"/> objects.</param>
        public ProvenBlockHeaderRepository(Network network, DataFolder folder, ILoggerFactory loggerFactory,
            DBreezeSerializer dBreezeSerializer)
        : this(network, folder.ProvenBlockHeaderPath, loggerFactory, dBreezeSerializer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderRepository"/> folder path to the DBreeze database files.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="dBreezeSerializer">The serializer to use for <see cref="IBitcoinSerializable"/> objects.</param>
        public ProvenBlockHeaderRepository(Network network, string folder, ILoggerFactory loggerFactory,
            DBreezeSerializer dBreezeSerializer)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));
            this.dBreezeSerializer = dBreezeSerializer;

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
        public Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            Task<ProvenBlockHeader> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(ProvenBlockHeaderTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>(ProvenBlockHeaderTable, blockHeight.ToBytes());

                    if (row.Exists)
                        return this.dBreezeSerializer.Deserialize<ProvenBlockHeader>(row.Value);

                    return null;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(SortedDictionary<int, ProvenBlockHeader> headers, HashHeightPair newTip)
        {
            Guard.NotNull(headers, nameof(headers));
            Guard.NotNull(newTip, nameof(newTip));

            Guard.Assert(newTip.Hash == headers.Values.Last().GetHash());

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

            transaction.Insert(BlockHashHeightTable, blockHashHeightKey, this.dBreezeSerializer.Serialize(newTip));
        }

        /// <summary>
        /// Inserts <see cref="ProvenBlockHeader"/> items into to the database.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <param name="headers"> List of <see cref="ProvenBlockHeader"/> items to save.</param>
        private void InsertHeaders(DBreeze.Transactions.Transaction transaction, SortedDictionary<int, ProvenBlockHeader> headers)
        {
            foreach (KeyValuePair<int, ProvenBlockHeader> header in headers)
                transaction.Insert(ProvenBlockHeaderTable, header.Key.ToBytes(), this.dBreezeSerializer.Serialize(header.Value));

            // Store the latest ProvenBlockHeader in memory.
            this.provenBlockHeaderTip = headers.Last().Value;
        }

        /// <summary>
        /// Retrieves the current <see cref="HashHeightPair"/> tip from disk.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <returns> Hash of blocks current tip.</returns>
        private HashHeightPair GetTipHash(DBreeze.Transactions.Transaction transaction)
        {
            HashHeightPair tipHash = null;

            Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>(BlockHashHeightTable, blockHashHeightKey);

            if (row.Exists)
                tipHash = this.dBreezeSerializer.Deserialize<HashHeightPair>(row.Value);

            return tipHash;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }
    }
}
