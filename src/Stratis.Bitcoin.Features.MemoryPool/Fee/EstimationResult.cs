using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Used to return detailed information about a fee estimate calculation
    /// </summary>
    public class EstimationResult
    {
        public EstimatorBucket Pass { get; set; }
        public EstimatorBucket Fail { get; set; }
        public double Decay { get; set; }
        public int Scale { get; set; }
    }
}
