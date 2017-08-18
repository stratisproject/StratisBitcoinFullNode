using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin
{
    public interface IFeatureStats
    {
        void AddFeatureStats(StringBuilder benchLog);
    }
}
