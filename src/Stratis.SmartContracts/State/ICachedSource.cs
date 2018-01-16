using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public interface ICachedSource<Key, Value>  : ISource<Key, Value>
    {
        ISource<Key, Value> GetSource();
        ICollection<Key> GetModified();
        bool HasModified();
        long EstimateCacheSize();
    }
}
