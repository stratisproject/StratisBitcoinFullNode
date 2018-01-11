using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Trie
{
    public interface ITrie<V> : ISource<byte[], V>
    {
        byte[] GetRootHash();

        void SetRoot(byte[] root);

        void Clear();
    }
}
