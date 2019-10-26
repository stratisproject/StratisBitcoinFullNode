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
        /// If consensus tip in IBD or store tip is a distance of more then 5 blocks from consensus tip this will return <c>false</c>.
        /// </para>
        /// </summary>
        bool ShouldFlush { get; }
    }
}
