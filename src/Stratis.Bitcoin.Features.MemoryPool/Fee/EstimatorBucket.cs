using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Used to return detailed information about a feerate bucket
    /// </summary>
    public class EstimatorBucket
    {
        public double Start { get; set; }
        public double End { get; set; }
        public double WithinTarget { get; set; }
        public double TotalConfirmed { get; set; }
        public double InMempool { get; set; }
        public double LeftMempool { get; set; }

        public EstimatorBucket()
        {
            this.Start = -1;
            this.End = -1;
        }
    }
}
