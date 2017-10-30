using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.AzureIndexer
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
            return this.comparer(x).Equals(this.comparer(y));
        }

        public int GetHashCode(T obj)
        {
            return this.comparer(obj).GetHashCode();
        }

        #endregion
    }
}
