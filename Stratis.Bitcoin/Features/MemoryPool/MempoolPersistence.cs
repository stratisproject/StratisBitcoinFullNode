﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public interface IMempoolPersistence
    {
        MemPoolSaveResult Save(TxMempool memPool, string fileName = null);
        IEnumerable<MempoolPersistenceEntry> Load(string fileName = null);
    }

    public struct MemPoolSaveResult
    {
        public static MemPoolSaveResult NonSuccess { get { return new MemPoolSaveResult { Succeeded = false }; } }
        public static MemPoolSaveResult Success(uint trxSaved) { return new MemPoolSaveResult { Succeeded = true, TrxSaved = trxSaved }; }

        public bool Succeeded { get; private set; }
        public uint TrxSaved { get; private set; }
    }

    public class MempoolPersistenceEntry : IBitcoinSerializable
    {
        Transaction tx;
        uint time;
        uint feeDelta;

        public Transaction Tx { get { return this.tx; } set { this.tx = value; } }
        public long Time { get { return (long)this.time; } set { this.time = (uint)value; } }
        public long FeeDelta { get { return (long)this.feeDelta; } set { this.feeDelta = (uint)value; } }

        public static MempoolPersistenceEntry FromTxMempoolEntry(TxMempoolEntry tx)
        {
            return new MempoolPersistenceEntry()
            {
                Tx = tx.Transaction,
                Time = tx.Time,
                FeeDelta = tx.feeDelta
            };
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.tx);
            stream.ReadWriteAsCompactVarInt(ref this.time);
            stream.ReadWriteAsCompactVarInt(ref this.feeDelta);
        }

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

        public override int GetHashCode()
        {
            return this.tx.GetHashCode();
        }

    }

    internal class MempoolPersistence : IMempoolPersistence
    {
        public const ulong MEMPOOL_DUMP_VERSION = 0;
        public const string defaultFilename = "mempool.dat";

        private readonly string dataDir;
        private readonly ILogger mempoolLogger;

        public MempoolPersistence(NodeSettings settings, ILoggerFactory loggerFactory)
        {
            this.dataDir = settings?.DataDir;
            this.mempoolLogger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public MemPoolSaveResult Save(TxMempool memPool, string fileName = null)
        {
            fileName = fileName ?? defaultFilename;
            IEnumerable<MempoolPersistenceEntry> toSave = memPool.MapTx.Values.ToArray().Select(tx => MempoolPersistenceEntry.FromTxMempoolEntry(tx));
            return this.Save(toSave, fileName);
        }

        internal MemPoolSaveResult Save(IEnumerable<MempoolPersistenceEntry> toSave, string fileName)
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
                            this.DumpToStream(toSave, fs);
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

        internal void DumpToStream(IEnumerable<MempoolPersistenceEntry> toSave, Stream stream)
        {
            var bitcoinWriter = new BitcoinStream(stream, true);

            bitcoinWriter.ReadWrite(MEMPOOL_DUMP_VERSION);
            bitcoinWriter.ReadWrite(toSave.LongCount());

            foreach (MempoolPersistenceEntry entry in toSave)
            {
                bitcoinWriter.ReadWrite(entry);
            }
        }

        public IEnumerable<MempoolPersistenceEntry> Load(string fileName = null)
        {
            fileName = fileName ?? defaultFilename;
            Guard.NotEmpty(this.dataDir, nameof(this.dataDir));
            Guard.NotEmpty(fileName, nameof(fileName));

            string filePath = Path.Combine(this.dataDir, fileName);
            if (!File.Exists(filePath))
                return null;
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open))
                    return this.LoadFromStream(fs);
            }
            catch (Exception ex)
            {
                this.mempoolLogger.LogError(ex.Message);
                throw;
            }
        }

        internal IEnumerable<MempoolPersistenceEntry> LoadFromStream(Stream stream)
        {
            var toReturn = new List<MempoolPersistenceEntry>();

            ulong version = 0;
            long numEntries = -1;
            var bitcoinReader = new BitcoinStream(stream, false);

            bool exitWithError = false;
            try
            {
                bitcoinReader.ReadWrite(ref version);
                if (version != MEMPOOL_DUMP_VERSION)
                {
                    this.mempoolLogger.LogWarning($"Memorypool data is wrong version ({version}) aborting...");
                    return null;
                }
                bitcoinReader.ReadWrite(ref numEntries);
            }
            catch
            {
                this.mempoolLogger.LogWarning($"Memorypool data is corrupt at header, aborting...");
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
                    this.mempoolLogger.LogWarning($"Memorypool data is corrupt at item {i + 1}, aborting...");
                    return null;
                }

                toReturn.Add(entry);
            }


            return toReturn;
        }

    }
}
