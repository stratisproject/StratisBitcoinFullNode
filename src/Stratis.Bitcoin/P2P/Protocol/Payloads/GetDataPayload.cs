using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Ask for transaction, block or merkle block.
    /// </summary>
    [Payload("getdata")]
    public class GetDataPayload : Payload
    {
        private List<InventoryVector> inventory = new List<InventoryVector>();

        public List<InventoryVector> Inventory { get { return this.inventory; } set { this.inventory = value; } }

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