using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ. Used to transform one datatype into another on the way in or out of a source.
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    /// <typeparam name="SourceKey"></typeparam>
    /// <typeparam name="SourceValue"></typeparam>
    public class SourceCodec<Key, Value, SourceKey, SourceValue> : AbstractChainedSource<Key, Value, SourceKey, SourceValue>
    {
        protected ISerializer<Key, SourceKey> keySerializer;
        protected ISerializer<Value, SourceValue> valSerializer;

        public SourceCodec(ISource<SourceKey, SourceValue> src, ISerializer<Key, SourceKey> keySerializer, ISerializer<Value, SourceValue> valSerializer) : base(src)
        {
            this.keySerializer = keySerializer;
            this.valSerializer = valSerializer;
            this.SetFlushSource(true);
        }

        public override void Put(Key key, Value val)
        {
            this.GetSource().Put(this.keySerializer.Serialize(key), this.valSerializer.Serialize(val));
        }

        public override Value Get(Key key)
        {
            return this.valSerializer.Deserialize(this.GetSource().Get(this.keySerializer.Serialize(key)));
        }

        public override void Delete(Key key)
        {
            this.GetSource().Delete(this.keySerializer.Serialize(key));
        }

        protected override bool FlushImpl()
        {
            return false;
        }
    }

}
