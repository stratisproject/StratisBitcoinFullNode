using System;
using System.Collections.Generic;

namespace NBitcoin.Protocol
{
    [Payload("getblocktxn")]
    public class GetBlockTxnPayload : Payload
    {
        private uint256 blockId = uint256.Zero;
        public uint256 BlockId { get { return this.blockId; } set { this.blockId = value; } }

        private List<int> indices = new List<int>();
        public List<int> Indices { get { return this.indices; } }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.blockId);
            ulong indexes_size = (ulong)this.indices.Count;
            stream.ReadWriteAsVarInt(ref indexes_size);

            if (!stream.Serializing)
            {
                ulong i = 0;
                ulong indicesCount = 0;
                while ((ulong)this.indices.Count < indexes_size)
                {
                    indicesCount = Math.Min(1000UL + (ulong)indicesCount, (ulong)indexes_size);
                    for (; i < indicesCount; i++)
                    {
                        ulong index = 0;
                        stream.ReadWriteAsVarInt(ref index);
                        if (index > int.MaxValue)
                            throw new FormatException("indexes overflowed 31-bits");

                        this.indices.Add((int)index);
                    }
                }

                int offset = 0;
                for (int ii = 0; ii < this.indices.Count; ii++)
                {
                    if ((ulong)(this.indices[ii]) + (ulong)(offset) > int.MaxValue)
                        throw new FormatException("indexes overflowed 31-bits");

                    this.indices[ii] = this.indices[ii] + offset;
                    offset = this.indices[ii] + 1;
                }
            }
            else
            {
                for (int i = 0; i < this.indices.Count; i++)
                {
                    int index = this.indices[i] - (i == 0 ? 0 : (this.indices[i - 1] + 1));
                    stream.ReadWrite(ref index);
                }
            }
        }
    }
}