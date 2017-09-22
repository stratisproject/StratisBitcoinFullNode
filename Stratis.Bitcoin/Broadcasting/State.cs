using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Broadcasting
{
    public enum State
    {
        CantBroadcast,
        ToBroadcast,
        Broadcasted,
        Propagated
    }
}
