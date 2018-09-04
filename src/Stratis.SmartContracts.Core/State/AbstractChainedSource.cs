using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ. 
    /// </summary>
    public abstract class AbstractChainedSource<Key, Value, SourceKey, SourceValue> : ISource<Key, Value>
    {
        public ISource<SourceKey, SourceValue> Source { get; protected set; }
        protected bool flushSource;

        protected AbstractChainedSource()
        {
        }

        public AbstractChainedSource(ISource<SourceKey, SourceValue> source)
        {
            this.Source = source;
        }

        public void SetFlushSource(bool flushSource)
        {
            this.flushSource = flushSource;
        }

        public virtual bool Flush()
        {
            bool ret = this.FlushImpl();
            if (this.flushSource)
                ret |= this.Source.Flush();
            return ret;
        }

        protected abstract bool FlushImpl();
        public abstract void Put(Key key, Value val);
        public abstract Value Get(Key key);
        public abstract void Delete(Key key);
    }
}
