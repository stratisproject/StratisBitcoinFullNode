using System.Net;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// Provides an optimal way of managing large collections of IPEndPoint objects.
    /// </summary>
    public class IPEndPointSet:HashSet<IPEndPoint>
    {
        /// <summary>
        /// Compares two IPEndPoint objects by first calling MapToIpv6().
        /// </summary>
        public class IPEndPointComparer:IEqualityComparer<IPEndPoint>
        {
            public int GetHashCode(IPEndPoint obj)
            {
                return obj.MapToIpv6().GetHashCode();
            }

            public bool Equals(IPEndPoint obj1, IPEndPoint obj2)
            {
                return obj1.MapToIpv6().Equals(obj2.MapToIpv6());
            }
        }
            
        /// <summary>
        /// Constructs an IPEndPointSet.
        /// </summary>
        /// <param name="list">An optional enumeration of IPEndPoint objects.</param>
        public IPEndPointSet(IEnumerable<IPEndPoint> list = null)
            :base(list ?? new List<IPEndPoint>(), new IPEndPointComparer())
        {
        }

        /// <summary>
        /// Adds an enumeration of IPEndPoint objects to the set.
        /// </summary>
        /// <param name="list"></param>
        public void AddRange(IEnumerable<IPEndPoint> list)
        {
            UnionWith(list);
        }
    }
}
