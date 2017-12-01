using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Announce the hash of a transaction or block.
    /// </summary>
    [Payload("inv")]
    public class InventoryPayload : Payload, IEnumerable<InventoryVector>
    {
        public const int MaxInventorySize = 50000;

        private List<InventoryVector> inventory = new List<InventoryVector>();
        public List<InventoryVector> Inventory { get { return this.inventory; } }

        public InventoryPayload(params Transaction[] transactions)
            : this(transactions.Select(tx => new InventoryVector(InventoryType.MSG_TX, tx.GetHash())).ToArray())
        {
        }

        public InventoryPayload(params Block[] blocks)
            : this(blocks.Select(b => new InventoryVector(InventoryType.MSG_BLOCK, b.GetHash())).ToArray())
        {
        }

        public InventoryPayload(params InventoryVector[] invs)
        {
            this.inventory.AddRange(invs);
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            var old = stream.MaxArraySize;
            stream.MaxArraySize = MaxInventorySize;
            stream.ReadWrite(ref this.inventory);
            stream.MaxArraySize = old;
        }

        public override string ToString()
        {
            return "Count: " + this.Inventory.Count;
        }

        public IEnumerator<InventoryVector> GetEnumerator()
        {
            return this.Inventory.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}