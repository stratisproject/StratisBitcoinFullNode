using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// A getdata message for an asked hash is not found by the remote peer.
    /// </summary>
    public class NotFoundPayload : Payload, IEnumerable<InventoryVector>
    {
        private List<InventoryVector> inventory = new List<InventoryVector>();

        public List<InventoryVector> Inventory { get { return this.inventory; } }

        public NotFoundPayload()
        {
        }

        public NotFoundPayload(params Transaction[] transactions)
            : this(transactions.Select(tx => new InventoryVector(InventoryType.MSG_TX, tx.GetHash())).ToArray())
        {
        }

        public NotFoundPayload(params Block[] blocks)
            : this(blocks.Select(b => new InventoryVector(InventoryType.MSG_BLOCK, b.GetHash())).ToArray())
        {
        }

        public NotFoundPayload(InventoryType type, params uint256[] hashes)
            : this(hashes.Select(h => new InventoryVector(type, h)).ToArray())
        {
        }

        public NotFoundPayload(params InventoryVector[] invs)
        {
            this.inventory.AddRange(invs);
        }

        #region IBitcoinSerializable Members

        public override void ReadWriteCore(BitcoinStream stream)
        {
            int old = stream.MaxArraySize;
            stream.MaxArraySize = 5000;
            stream.ReadWrite(ref this.inventory);
            stream.MaxArraySize = old;
        }

        #endregion

        public override string ToString()
        {
            return "Count: " + this.Inventory.Count.ToString();
        }

        public IEnumerator<InventoryVector> GetEnumerator()
        {
            return this.Inventory.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
