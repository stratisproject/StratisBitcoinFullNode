using System.Collections.Generic;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// </summary>
    public interface ICachedSource<Key, Value>  : ISource<Key, Value>
    {
        ISource<Key, Value> Source { get; }
        ICollection<Key> GetModified();
        bool HasModified();
    }
}
