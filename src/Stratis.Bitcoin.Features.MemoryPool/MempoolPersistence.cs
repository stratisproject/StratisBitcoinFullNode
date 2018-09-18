using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Public interface for persisting the memory pool.
    /// </summary>
    public interface IMempoolPersistence
    {
        /// <summary>
        /// Persists the memory pool to a file.
        /// </summary>
        /// <param name="memPool">The transaction memory pool.</param>
        /// <param name="fileName">The filename to persist to. Default filename is used if null.</param>
        /// <returns>Result of saving the memory pool.</returns>
        MemPoolSaveResult Save(Network network, ITxMempool memPool, string fileName = null);

        /// <summary>
        /// Loads the memory pool from a persisted file.
        /// </summary>
        /// <param name="fileName">Filename to load from. Default filename is used if null.</param>
        /// <returns>List of persistence entries.</returns>
        IEnumerable<MempoolPersistenceEntry> Load(Network network, string fileName = null);
    }

    /// <summary>
    /// The result of a memory pool save.
    /// </summary>
    public struct MemPoolSaveResult
    {
        /// <summary>Gets a non successful save result.</summary>
        public static MemPoolSaveResult NonSuccess
        {
            get { return new MemPoolSaveResult { Succeeded = false }; }
        }

        /// <summary>
        /// Defines a successful save result.
        /// </summary>
        /// <param name="trxSaved">The transaction that was saved.</param>
        /// <returns>A successful save result.</returns>
        public static MemPoolSaveResult Success(uint trxSaved)
        {
            return new MemPoolSaveResult { Succeeded = true, TrxSaved = trxSaved };
        }

        /// <summary>Whether the file save was successful.</summary>
        public bool Succeeded { get; private set; }

        /// <summary>The transaction id that was saved.</summary>
        public uint TrxSaved { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0}:{1},{2}:{3}", nameof(this.Succeeded), this.Succeeded, nameof(this.TrxSaved), this.TrxSaved);
        }
    }

    /// <summary>
    /// A memory pool entry to be persisted.
    /// </summary>
    public class MempoolPersistenceEntry : IBitcoinSerializable
    {
        /// <summary>Memory pool transaction to persist.</summary>
        private Transaction tx;

        /// <summary>The transaction time.</summary>
        private uint time;

        /// <summary>The transaction fee difference.</summary>
        private uint feeDelta;

        /// <summary>Gets or set the transaction for persistence.</summary>
        public Transaction Tx
        {
            get { return this.tx; }
            set { this.tx = value; }
        }

        /// <summary>Gets or sets the memory pools time for the transaction for persistence.</summary>
        public long Time
        {
            get { return (long)this.time; }
            set { this.time = (uint)value; }
        }

        /// <summary>Gets or sets the transaction fee difference for persistence.</summary>
        public long FeeDelta
        {
            get { return (long)this.feeDelta; }
            set { this.feeDelta = (uint)value; }
        }

        /// <summary>
        /// Creates a persistence entry from a memory pool transaction entry.
        /// </summary>
        /// <param name="tx">Memory pool transaction entry.</param>
        /// <returns>Persistence entry.</returns>
        public static MempoolPersistenceEntry FromTxMempoolEntry(TxMempoolEntry tx)
        {
            return new MempoolPersistenceEntry
            {
                Tx = tx.Transaction,
                Time = tx.Time,
                FeeDelta = tx.feeDelta
            };
        }

        /// <summary>
        /// Does a readwrite to the stream of this persistence entry.
        /// </summary>
        /// <param name="stream">Stream to do readwrite to.</param>
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.tx);
            stream.ReadWriteAsCompactVarInt(ref this.time);
            stream.ReadWriteAsCompactVarInt(ref this.feeDelta);
        }

        /// <summary>
        /// Compares whether two persistence entries are equal.
        /// </summary>
        /// <param name="obj">Object to compare this persistence entry to.</param>
        /// <returns>Whether the objects are equal.</returns>
        public override bool Equals(object obj)
        {
            var toCompare = obj as MempoolPersistenceEntry;
            if (toCompare == null) return false;

            if ((this.tx == null) != (toCompare.tx == null))
                return false;

            if (!this.time.Equals(toCompare.time) || !this.feeDelta.Equals(toCompare.feeDelta))
                return false;

            if ((this.tx == null) && (toCompare.tx == null))
                return true;

            return this.tx.ToHex().Equals(toCompare.tx.ToHex());
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.tx.GetHashCode();
        }
    }

    /// <summary>
    /// Object used for persisting memory pool transactions.
    /// </summary>
    internal class MempoolPersistence : IMempoolPersistence
    {
        /// <summary>Current memory pool version number for persistence.</summary>
        public const ulong MempoolDumpVersion = 0;

        /// <summary>The default filename used for memory pool persistence.</summary>
        public const string DefaultFilename = "mempool.dat";

        /// <summary>Data directory to save persisted memory pool to.</summary>
        private readonly string dataDir;

        /// <summary>Instance logger for the memory pool persistence object.</summary>
        private readonly ILogger mempoolLogger;

        /// <summary>
        /// Constructs an instance of an object for persisting memory pool transactions.
        /// </summary>
        /// <param name="settings">Node settings used for getting the data directory.</param>
        /// <param name="loggerFactory">Logger factory for creating instance logger.</param>
        public MempoolPersistence(NodeSettings settings, ILoggerFactory loggerFactory)
        {
            this.dataDir = settings?.DataDir;
            this.mempoolLogger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public MemPoolSaveResult Save(Network network, ITxMempool memPool, string fileName = null)
        {
            fileName = fileName ?? DefaultFilename;
            IEnumerable<MempoolPersistenceEntry> toSave = memPool.MapTx.Values.ToArray().Select(tx => MempoolPersistenceEntry.FromTxMempoolEntry(tx));
            return this.Save(network, toSave, fileName);
        }

        /// <summary>
        /// Saves a list of memory pool transaction entries to a persistence file.
        /// </summary>
        /// <param name="network">The blockchain network.</param>
        /// <param name="toSave">List of persistence transactions to save.</param>
        /// <param name="fileName">The filename to persist transactions to.</param>
        /// <returns>The save result.</returns>
        internal MemPoolSaveResult Save(Network network, IEnumerable<MempoolPersistenceEntry> toSave, string fileName)
        {
            Guard.NotEmpty(this.dataDir, nameof(this.dataDir));
            Guard.NotEmpty(fileName, nameof(fileName));

            string filePath = Path.Combine(this.dataDir, fileName);
            string tempFilePath = $"{fileName}.new";

            if (Directory.Exists(this.dataDir))
            {
                try
                {
                    if (!toSave.Any())
                    {
                        File.Delete(filePath);
                    }
                    else
                    {
                        using (var fs = new FileStream(tempFilePath, FileMode.Create))
                        {
                            this.DumpToStream(network, toSave, fs);
                        }
                        File.Delete(filePath);
                        File.Move(tempFilePath, filePath);
                    }
                    return MemPoolSaveResult.Success((uint)toSave.LongCount());
                }
                catch (Exception ex)
                {
                    this.mempoolLogger.LogError(ex.Message);
                    throw;
                }
            }

            return MemPoolSaveResult.NonSuccess;
        }

        /// <summary>
        /// Writes a collection of memory pool transactions to a stream.
        /// </summary>
        /// <param name="network">The blockchain network.</param>
        /// <param name="toSave">Collection of memory pool transactions to save.</param>
        /// <param name="stream">Stream to write transactions to.</param>
        internal void DumpToStream(Network network, IEnumerable<MempoolPersistenceEntry> toSave, Stream stream)
        {
            var bitcoinWriter = new BitcoinStream(stream, true);
            bitcoinWriter.ConsensusFactory = network.Consensus.ConsensusFactory;

            bitcoinWriter.ReadWrite(MempoolDumpVersion);
            bitcoinWriter.ReadWrite(toSave.LongCount());

            foreach (MempoolPersistenceEntry entry in toSave)
            {
                bitcoinWriter.ReadWrite(entry);
            }
        }

        /// <inheritdoc />
        public IEnumerable<MempoolPersistenceEntry> Load(Network network, string fileName = null)
        {
            fileName = fileName ?? DefaultFilename;
            Guard.NotEmpty(this.dataDir, nameof(this.dataDir));
            Guard.NotEmpty(fileName, nameof(fileName));

            string filePath = Path.Combine(this.dataDir, fileName);
            if (!File.Exists(filePath))
                return null;
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    return this.LoadFromStream(network, fs);
                }
            }
            catch (Exception ex)
            {
                this.mempoolLogger.LogError(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Loads a collection of memory pool transactions from a persistence stream.
        /// </summary>
        /// <param name="stream">Stream to load transactions from.</param>
        /// <returns>Collection of memory pool transactions.</returns>
        internal IEnumerable<MempoolPersistenceEntry> LoadFromStream(Network network, Stream stream)
        {
            var toReturn = new List<MempoolPersistenceEntry>();

            ulong version = 0;
            long numEntries = -1;
            var bitcoinReader = new BitcoinStream(stream, false);
            bitcoinReader.ConsensusFactory = network.Consensus.ConsensusFactory;
            bool exitWithError = false;
            try
            {
                bitcoinReader.ReadWrite(ref version);
                if (version != MempoolDumpVersion)
                {
                    this.mempoolLogger.LogWarning($"Memorypool data is wrong version ({version}) aborting.");
                    return null;
                }
                bitcoinReader.ReadWrite(ref numEntries);
            }
            catch
            {
                this.mempoolLogger.LogWarning($"Memorypool data is corrupt at header, aborting.");
                return null;
            }

            for (int i = 0; i < numEntries && !exitWithError; i++)
            {
                MempoolPersistenceEntry entry = default(MempoolPersistenceEntry);
                try
                {
                    bitcoinReader.ReadWrite(ref entry);
                }
                catch
                {
                    this.mempoolLogger.LogWarning($"Memorypool data is corrupt at item {i + 1}, aborting.");
                    return null;
                }

                toReturn.Add(entry);
            }

            return toReturn;
        }
    }
}
