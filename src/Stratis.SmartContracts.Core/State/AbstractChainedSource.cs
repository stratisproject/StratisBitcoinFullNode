using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ. 
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    /// <typeparam name="SourceKey"></typeparam>
    /// <typeparam name="SourceValue"></typeparam>
    public abstract class AbstractChainedSource<Key, Value, SourceKey, SourceValue> : ISource<Key, Value>
    {
        private ISource<SourceKey, SourceValue> source;
        protected bool flushSource;

        protected AbstractChainedSource()
        {
        }

        public AbstractChainedSource(ISource<SourceKey, SourceValue> source)
        {
            this.source = source;
        }

        protected void SetSource(ISource<SourceKey, SourceValue> src)
        {
            this.source = src;
        }

        public ISource<SourceKey, SourceValue> GetSource()
        {
            return this.source;
        }

        public void SetFlushSource(bool flushSource)
        {
            this.flushSource = flushSource;
        }

        public virtual bool Flush()
        {
            bool ret = this.FlushImpl();
            if (this.flushSource)
                ret |= this.GetSource().Flush();
            return ret;
        }

        protected abstract bool FlushImpl();
        public abstract void Put(Key key, Value val);
        public abstract Value Get(Key key);
        public abstract void Delete(Key key);
    }
}
