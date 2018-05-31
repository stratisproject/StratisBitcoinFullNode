using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Identifier for each of the 3 different TxConfirmStats which will track
    /// history over different time horizons
    /// </summary>
    public enum FeeEstimateHorizon
    {
        ShortHalfLife,
        MedHalfLife,
        LongHalfLife
    }
}
