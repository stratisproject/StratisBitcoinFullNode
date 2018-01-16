using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class SourceCodec<Key, Value, SourceKey, SourceValue> : AbstractChainedSource<Key, Value, SourceKey, SourceValue>
    {
        protected ISerializer<Key, SourceKey> keySerializer;
        protected ISerializer<Value, SourceValue> valSerializer;

        public SourceCodec(ISource<SourceKey, SourceValue> src, ISerializer<Key, SourceKey> keySerializer, ISerializer<Value, SourceValue> valSerializer) : base(src)
        {
            this.keySerializer = keySerializer;
            this.valSerializer = valSerializer;
            SetFlushSource(true);
        }

        public override void Put(Key key, Value val)
        {
            GetSource().Put(keySerializer.Serialize(key), valSerializer.Serialize(val));
        }

        public override Value Get(Key key)
        {
            return valSerializer.Deserialize(GetSource().Get(keySerializer.Serialize(key)));
        }

        public override void Delete(Key key)
        {
            GetSource().Delete(keySerializer.Serialize(key));
        }

        protected override bool FlushImpl()
        {
            return false;
        }
    }

}
