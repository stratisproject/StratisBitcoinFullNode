namespace Stratis.Bitcoin.Interfaces
{
    /// <summary>
    /// Determines whether or not <see cref="BlockStoreQueue"/> should flush it's batch to disk.
    /// </summary>
    public interface IBlockStoreQueueFlushCondition
    {
        /// <summary>
        /// Should block store flush to disk.
        /// <para>
        /// This is usually performed by checking <see cref="Base.IChainState.IsAtBestChainTip"/>.
        /// </para>
        /// </summary>
        bool ShouldFlush { get; }
    }
}
