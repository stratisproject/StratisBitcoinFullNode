using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class SourceChainBox<Key, Value, SourceKey, SourceValue> : AbstractChainedSource<Key, Value, SourceKey, SourceValue>
    {

        List<ISource<Key, Value>> chain = new List<ISource<Key, Value>>();
        ISource<Key, Value> lastSource;

        public SourceChainBox(ISource<SourceKey, SourceValue> source) : base(source)
        {
        }


        public void Add(ISource<Key, Value> src)
        {
            this.chain.Add(src);
            lastSource = src;
        }

        public override void Put(Key key, Value val)
        {
            lastSource.Put(key, val);
        }

        public override Value Get(Key key)
        {
            return lastSource.Get(key);
        }

        public override void Delete(Key key)
        {
            lastSource.Delete(key);
        }

        protected override bool FlushImpl()
        {
            return lastSource.Flush();
        }
    }
}
