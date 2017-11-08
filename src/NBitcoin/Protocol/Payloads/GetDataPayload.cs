using System.Collections.Generic;

namespace NBitcoin.Protocol
{
    /// <summary>
    /// Ask for transaction, block or merkle block.
    /// </summary>
    [Payload("getdata")]
    public class GetDataPayload : Payload
    {
        private List<InventoryVector> inventory = new List<InventoryVector>();
        public List<InventoryVector> Inventory { set { this.inventory = value; } get { return this.inventory; } }

        public GetDataPayload()
        {
        }

        public GetDataPayload(params InventoryVector[] vectors)
        {
            this.inventory.AddRange(vectors);
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.inventory);
        }
    }
}