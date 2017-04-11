using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using System.IO;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Logging;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.MemoryPool
{
    public interface IMempoolPersistence
    {
        Task<MemPoolSaveResult> Save(TxMempool memPool);
    }

    public struct MemPoolSaveResult
    {
        public static MemPoolSaveResult NonSuccess { get { return new MemPoolSaveResult { Succeeded = false }; } }
        public static MemPoolSaveResult Success(uint trxSaved) { return new MemPoolSaveResult { Succeeded = true, TrxSaved = trxSaved }; }

        public bool Succeeded { get; private set; }
        public uint TrxSaved { get; private set; }
    }

    internal class MempoolPersistenceEntry : IBitcoinSerializable
    {
        uint256 tx;
        uint time;
        uint feeDelta;

        public uint256 Tx { get { return this.tx; } set { this.tx = value; } }
        public long Time { get { return (long)this.time; } set { this.time = (uint)value; } }
        public long FeeDelta { get { return (long)this.feeDelta; } set { this.feeDelta = (uint)value; } }

        public static MempoolPersistenceEntry FromTxMempoolEntry(TxMempoolEntry tx)
        {
            return new MempoolPersistenceEntry()
            {
                Tx = tx.TransactionHash,
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
            return this.tx.Equals(toCompare.tx) 
                && this.time.Equals(toCompare.time)
                && this.feeDelta.Equals(toCompare.feeDelta);
        }

    }

    internal class MempoolPersistence : IMempoolPersistence
    {
        public const ulong MEMPOOL_DUMP_VERSION = 1;

        public string DataDir;

        public MempoolPersistence(NodeSettings settings)
        {
            this.DataDir = settings?.DataDir;
        }


        public async Task<MemPoolSaveResult> Save(TxMempool memPool)
        {
            Guard.NotEmpty(this.DataDir, nameof(DataDir));

            if (Directory.Exists(this.DataDir))
            {

                IEnumerable<MempoolPersistenceEntry> toSave = memPool.MapTx.Values.ToArray().Select(tx => MempoolPersistenceEntry.FromTxMempoolEntry(tx));

                try
                {
                    string filePath = Path.Combine(this.DataDir, "mempool.dat");
                    string tempFilePath = $"{filePath}.dat";
                    using (var fs = new FileStream(tempFilePath, FileMode.Create))
                    {
                        DumpToStream(toSave, fs);
                    }
                    File.Move(tempFilePath, filePath);
                    return MemPoolSaveResult.Success((uint)toSave.LongCount());
                }
                catch (Exception ex)
                {
                    Logs.Mempool.LogError(ex.Message);
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
                    Logs.Mempool.LogWarning($"Memorypool data is wrong version ({version}) aborting...");
                    return null;
                }
                bitcoinReader.ReadWrite(ref numEntries);
            }
            catch
            {
                Logs.Mempool.LogWarning($"Memorypool data is corrupt at header, aborting...");
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
                    Logs.Mempool.LogWarning($"Memorypool data is corrupt at item {i + 1}, aborting...");
                    return null;
                }

                toReturn.Add(entry);
            }


            return toReturn;
        }

    }
}
