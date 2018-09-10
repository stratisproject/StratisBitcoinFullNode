using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// 
    /// Should be used in cases where you don't want to get rid of previous states. I.e. all contract storage changes.
    /// </summary>
    public class NoDeleteSource<Key, Value> : AbstractChainedSource<Key, Value, Key, Value>
    {
        public NoDeleteSource(ISource<Key, Value> src) : base(src)
        {
            this.SetFlushSource(true);
        }

        public override void Delete(Key key)
        {
        }

        public override void Put(Key key, Value val)
        {
            if (val != null)
                this.Source.Put(key, val);
        }

        public override Value Get(Key key)
        {
            return this.Source.Get(key);
        }

        protected override bool FlushImpl()
        {
            return false;
        }
    }

    /// <summary>
    /// Used for dependency injection. A contract state specific implementation of the above class.
    /// </summary>
    public class NoDeleteContractStateSource : NoDeleteSource<byte[], byte[]>
    {
        public NoDeleteContractStateSource(DBreezeContractStateStore src) : base(src) {}
    }
}
