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

    internal class MempoolPersistenceEntry: IBitcoinSerializable
    {
        public uint256 Tx { get; set; }
        public long Time { get; set; }
        public long Feedelta { get; set; }

        public static MempoolPersistenceEntry FromTxMempoolEntry(TxMempoolEntry tx)
        {
            return new MempoolPersistenceEntry()
            {
                Tx = tx.TransactionHash,
                Time = tx.Time,
                Feedelta = tx.feeDelta
            };
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(this.Tx);
            stream.ReadWrite(this.Time);
            stream.ReadWrite(this.Feedelta);
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
                        var bs = new BitcoinStream(fs, true);
                        DumpToStream(toSave, bs);
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

        internal void DumpToStream(IEnumerable<MempoolPersistenceEntry> toSave, BitcoinStream stream)
        {
            stream.ReadWrite(MEMPOOL_DUMP_VERSION);
            stream.ReadWrite(toSave.LongCount());

            foreach (MempoolPersistenceEntry entry in toSave)
            {
                stream.ReadWrite(entry);
            }
        }

    }
}
