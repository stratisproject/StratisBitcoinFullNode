using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee.SerializationEntity
{
    public class TxConfirmData
    {
        public double Decay { get; set; }
        public int Scale { get; set; }
        public List<double> Avg { get; set; }
        public List<double> TxCtAvg { get; set; }
        public List<List<double>> ConfAvg { get; set; }
        public List<List<double>> FailAvg { get; set; }
    }
}
