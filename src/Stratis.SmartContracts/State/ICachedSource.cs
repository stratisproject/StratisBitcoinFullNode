using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    public interface ICachedSource<Key, Value>  : ISource<Key, Value>
    {
        ISource<Key, Value> GetSource();
        ICollection<Key> GetModified();
        bool HasModified();
        long EstimateCacheSize();
    }
}
