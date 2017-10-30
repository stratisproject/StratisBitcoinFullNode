using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    class AnonymousEqualityComparer<T,TComparer> : IEqualityComparer<T>
    {
        Func<T, TComparer> comparer;
        public AnonymousEqualityComparer(Func<T,TComparer> comparer)
        {
            this.comparer = comparer;
        }
        #region IEqualityComparer<T> Members

        public bool Equals(T x, T y)
        {
            return comparer(x).Equals(comparer(y));
        }

        public int GetHashCode(T obj)
        {
            return comparer(obj).GetHashCode();
        }

        #endregion
    }
}
