using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin
{
    public interface INodeStats
    {
        void AddNodeStats(StringBuilder benchLog);
    }
}
