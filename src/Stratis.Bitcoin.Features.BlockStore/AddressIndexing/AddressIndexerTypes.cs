using System.Collections.Generic;
using LiteDB;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    public class AddressIndexerTipData
    {
        [BsonId]
        public int Id { get; set; }

        public byte[] TipHashBytes { get; set; }

        public int Height { get; set; }

        public override string ToString()
        {
            return $"{nameof(this.Height)}:{this.Height}";
        }
    }

    public class OutPointData
    {
        [BsonId]
        public string Outpoint { get; set; }

        public byte[] ScriptPubKeyBytes { get; set; }

        public long Money { get; set; }
    }

    public class AddressIndexerRewindData
    {
        [BsonId(false)]
        public string BlockHash { get; set; }

        public int BlockHeight { get; set; }

        public List<OutPointData> SpentOutputs { get; set; }
    }
}
