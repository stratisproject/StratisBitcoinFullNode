using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// Should be used in cases where you don't want to get rid of previous states. I.e. all contract storage changes.
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    public class NoDeleteSource<Key, Value> : AbstractChainedSource<Key, Value, Key, Value>
    {
        public NoDeleteSource(ISource<Key, Value> src) : base(src)
        {
            SetFlushSource(true);
        }

        public override void Delete(Key key)
        {
        }

        public override void Put(Key key, Value val)
        {
            if (val != null) GetSource().Put(key, val);
        }

        public override Value Get(Key key)
        {
            return GetSource().Get(key);
        }

        protected override bool FlushImpl()
        {
            return false;
        }
    }
}
