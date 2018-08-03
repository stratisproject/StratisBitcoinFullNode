using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Announce the hash of a transaction or block.
    /// </summary>
    [Payload("inv")]
    public class InvPayload : Payload, IEnumerable<InventoryVector>
    {
        /// <summary>Maximal number of inventory items in response to "getblocks" message.</summary>
        public const int MaxGetBlocksInventorySize = 500;

        public const int MaxInventorySize = 50000;

        private List<InventoryVector> inventory = new List<InventoryVector>();

        public List<InventoryVector> Inventory { get { return this.inventory; } }

        public InvPayload()
        {
        }

        public InvPayload(params Transaction[] transactions)
            : this(transactions.Select(tx => new InventoryVector(InventoryType.MSG_TX, tx.GetHash())).ToArray())
        {
        }

        public InvPayload(params Block[] blocks)
            : this(blocks.Select(b => new InventoryVector(InventoryType.MSG_BLOCK, b.GetHash())).ToArray())
        {
        }

        public InvPayload(params InventoryVector[] invs)
        {
            this.inventory.AddRange(invs);
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            int old = stream.MaxArraySize;
            stream.MaxArraySize = MaxInventorySize;
            stream.ReadWrite(ref this.inventory);
            stream.MaxArraySize = old;
        }

        public override string ToString()
        {
            return $"Count: {this.Inventory.Count}";
        }

        public IEnumerator<InventoryVector> GetEnumerator()
        {
            return this.Inventory.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}