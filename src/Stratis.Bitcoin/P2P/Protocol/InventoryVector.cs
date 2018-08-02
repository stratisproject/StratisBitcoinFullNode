using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Protocol
{
    public enum InventoryType : uint
    {
        Error = 0,
        MSG_TX = 1,
        MSG_BLOCK = 2,
        // Nodes may always request a MSG_FILTERED_BLOCK/MSG_CMPCT_BLOCK in a getdata, however,
        // MSG_FILTERED_BLOCK/MSG_CMPCT_BLOCK should not appear in any invs except as a part of getdata.
        MSG_FILTERED_BLOCK = 3,
        MSG_CMPCT_BLOCK,
        // The following can only occur in getdata. Invs always use TX or BLOCK.
        MSG_TYPE_MASK = 0xffffffff >> 2,
        MSG_WITNESS_FLAG = 1 << 30,
        MSG_WITNESS_BLOCK = MSG_BLOCK | MSG_WITNESS_FLAG,
        MSG_WITNESS_TX = MSG_TX | MSG_WITNESS_FLAG,
        MSG_FILTERED_WITNESS_BLOCK = MSG_FILTERED_BLOCK | MSG_WITNESS_FLAG
    }

    public class InventoryVector : Payload, IBitcoinSerializable
    {
        private uint type;

        public InventoryType Type
        {
            get
            {
                return (InventoryType)this.type;
            }

            set
            {
                this.type = (uint)value;
            }
        }

        private uint256 hash = uint256.Zero;

        public uint256 Hash { get { return this.hash; } set { this.hash = value; } }

        public InventoryVector()
        {
        }

        public InventoryVector(InventoryType type, uint256 hash)
        {
            this.Type = type;
            this.Hash = hash;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.type);
            stream.ReadWrite(ref this.hash);
        }

        public override string ToString()
        {
            return this.Type.ToString();
        }
    }
}
