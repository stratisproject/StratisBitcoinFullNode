using System.Collections.Generic;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// </summary>
    public abstract class AbstractCachedSource<Key, Value> : AbstractChainedSource<Key, Value, Key, Value>, ICachedSource<Key, Value>
    {
        public interface IEntry<V>
        {
            V Value();
        }

        public class SimpleEntry<V> : IEntry<V>
        {
            private V val;

            public SimpleEntry(V val)
            {
                this.val = val;
            }

            public V Value()
            {
                return this.val;
            }
        }

        public AbstractCachedSource(ISource<Key, Value> source) : base(source)
        {
        }

        public abstract IEntry<Value> GetCached(Key key);

        public abstract ICollection<Key> GetModified();
        public abstract bool HasModified();
    }
}
