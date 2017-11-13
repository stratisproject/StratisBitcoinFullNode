using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin.Crypto;

namespace NBitcoin.Protocol
{
    [Payload("cmpctblock")]
    public class CmpctBlockPayload : Payload
    {
        private BlockHeader header;
        public BlockHeader Header
        {
            get
            {
                return this.header;
            }
            set
            {
                this.header = value;
                if (value != null)
                    this.UpdateShortTxIDSelector();
            }
        }

        private ulong nonce;
        public ulong Nonce
        {
            get
            {
                return this.nonce;
            }
            set
            {
                this.nonce = value;
                this.UpdateShortTxIDSelector();
            }
        }

        private List<ulong> shortIds = new List<ulong>();
        public List<ulong> ShortIds { get { return this.shortIds; } }

        private List<PrefilledTransaction> prefilledTransactions = new List<PrefilledTransaction>();
        public List<PrefilledTransaction> PrefilledTransactions { get { return this.prefilledTransactions; } }

        private ulong shortTxidk0;
        private ulong shortTxidk1;

        public CmpctBlockPayload()
        {
        }

        public CmpctBlockPayload(Block block)
        {
            this.header = block.Header;
            this.nonce = RandomUtils.GetUInt64();
            this.UpdateShortTxIDSelector();
            this.PrefilledTransactions.Add(new PrefilledTransaction()
            {
                Index = 0,
                Transaction = block.Transactions[0]
            });

            foreach (Transaction tx in block.Transactions.Skip(1))
            {
                this.ShortIds.Add(GetShortID(tx.GetHash()));
            }
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.header);
            stream.ReadWrite(ref this.nonce);

            uint shortTxIds_size = (uint)this.shortIds.Count;
            stream.ReadWriteAsVarInt(ref shortTxIds_size);
            if (!stream.Serializing)
            {
                ulong i = 0;
                ulong shortTxIdsCount = 0;
                while (this.shortIds.Count < shortTxIds_size)
                {
                    shortTxIdsCount = Math.Min(1000UL + (ulong)shortTxIdsCount, (ulong)shortTxIds_size);
                    for (; i < shortTxIdsCount; i++)
                    {
                        uint lsb = 0;
                        ushort msb = 0;
                        stream.ReadWrite(ref lsb);
                        stream.ReadWrite(ref msb);
                        this.shortIds.Add(((ulong)(msb) << 32) | (ulong)(lsb));
                    }
                }
            }
            else
            {
                for (int i = 0; i < this.shortIds.Count; i++)
                {
                    uint lsb = (uint)(this.shortIds[i] & 0xffffffff);
                    ushort msb = (ushort)((this.shortIds[i] >> 32) & 0xffff);
                    stream.ReadWrite(ref lsb);
                    stream.ReadWrite(ref msb);
                }
            }

            ulong txn_size = (ulong)this.PrefilledTransactions.Count;
            stream.ReadWriteAsVarInt(ref txn_size);

            if (!stream.Serializing)
            {
                ulong i = 0;
                ulong indicesCount = 0;
                while ((ulong)this.PrefilledTransactions.Count < txn_size)
                {
                    indicesCount = Math.Min(1000UL + (ulong)indicesCount, (ulong)txn_size);
                    for (; i < indicesCount; i++)
                    {
                        ulong index = 0;
                        stream.ReadWriteAsVarInt(ref index);
                        if (index > int.MaxValue)
                            throw new FormatException("indexes overflowed 32-bits");
                        Transaction tx = null;
                        stream.ReadWrite(ref tx);
                        this.PrefilledTransactions.Add(new PrefilledTransaction()
                        {
                            Index = (int)index,
                            Transaction = tx
                        });
                    }
                }

                int offset = 0;
                for (int ii = 0; ii < this.PrefilledTransactions.Count; ii++)
                {
                    if ((ulong)(this.PrefilledTransactions[ii].Index) + (ulong)(offset) > int.MaxValue)
                        throw new FormatException("indexes overflowed 31-bits");
                    this.PrefilledTransactions[ii].Index = this.PrefilledTransactions[ii].Index + offset;
                    offset = this.PrefilledTransactions[ii].Index + 1;
                }
            }
            else
            {
                for (int i = 0; i < this.PrefilledTransactions.Count; i++)
                {
                    uint index = checked((uint)(this.PrefilledTransactions[i].Index - (i == 0 ? 0 : (this.PrefilledTransactions[i - 1].Index + 1))));
                    stream.ReadWriteAsVarInt(ref index);
                    Transaction tx = this.PrefilledTransactions[i].Transaction;
                    stream.ReadWrite(ref tx);
                }
            }

            if (!stream.Serializing)
                this.UpdateShortTxIDSelector();
        }

        private void UpdateShortTxIDSelector()
        {
            MemoryStream ms = new MemoryStream();
            BitcoinStream stream = new BitcoinStream(ms, true);
            stream.ReadWrite(ref this.header);
            stream.ReadWrite(ref this.nonce);
            uint256 shorttxidhash = new uint256(Hashes.SHA256(ms.ToArrayEfficient()));
            this.shortTxidk0 = Hashes.SipHasher.GetULong(shorttxidhash, 0);
            this.shortTxidk1 = Hashes.SipHasher.GetULong(shorttxidhash, 1);
        }

        public ulong AddTransactionShortId(Transaction tx)
        {
            return AddTransactionShortId(tx.GetHash());
        }

        public ulong AddTransactionShortId(uint256 txId)
        {
            ulong id = GetShortID(txId);
            this.ShortIds.Add(id);
            return id;
        }

        public ulong GetShortID(uint256 txId)
        {
            return Hashes.SipHash(this.shortTxidk0, this.shortTxidk1, txId) & 0xffffffffffffL;
        }
    }

    public class PrefilledTransaction
    {
        public Transaction Transaction { get; set; }

        public int Index { get; set; }
    }
}