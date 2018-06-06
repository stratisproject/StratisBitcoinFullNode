using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee.SerializationEntity
{
    public class BlockPolicyData
    {
        public int BestSeenHeight { get; set; }
        public int HistoricalFirst  { get; set; }
        public int HistoricalBest { get; set; }
        public List<double> Buckets { get; set; }
        public TxConfirmData ShortStats { get; set; }
        public TxConfirmData MedStats { get; set; }
        public TxConfirmData LongStats { get; set; }
    }
}
