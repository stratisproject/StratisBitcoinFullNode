using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public interface ISource<K,V>
    {
        void Put(K key, V val);

        V Get(K key);

        void Delete(K key);

        // Pushes changes through to underlying cache.
        bool Flush();
    }
}
