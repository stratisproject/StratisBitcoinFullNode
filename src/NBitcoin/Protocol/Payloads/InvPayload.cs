using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Protocol
{
    /// <summary>
    /// Announce the hash of a transaction or block.
    /// </summary>
    [Payload("inv")]
    public class InvPayload : Payload, IBitcoinSerializable, IEnumerable<InventoryVector>
    {
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

        public InvPayload(InventoryType type, params uint256[] hashes)
            : this(hashes.Select(h => new InventoryVector(type, h)).ToArray())
        {
        }

        public InvPayload(params InventoryVector[] invs)
        {
            this.inventory.AddRange(invs);
        }

        #region IBitcoinSerializable Members

        public override void ReadWriteCore(BitcoinStream stream)
        {
            var old = stream.MaxArraySize;
            stream.MaxArraySize = MaxInventorySize;
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

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}