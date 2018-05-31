using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    public class FeeCalculation
    {
        public EstimationResult Estimation { get; set; }
        public FeeReason Reason { get; set; }
        public int DesiredTarget { get; set; }
        public int ReturnedTarget { get; set; }
    }
}
