using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Broadcasting
{
    public enum State
    {
        CantBroadcast,
        ToBroadcast,
        Broadcasted,
        Propagated
    }
}
