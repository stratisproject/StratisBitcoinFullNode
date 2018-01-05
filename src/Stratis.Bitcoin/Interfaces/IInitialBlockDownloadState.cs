namespace Stratis.Bitcoin.Interfaces
{
    /// <summary>
    /// Provides IBD (Initial Block Download) state.
    /// </summary>
    public interface IInitialBlockDownloadState
    {
        /// <summary>
        /// Checks whether the node is currently in the process of initial block download.
        /// </summary>
        /// <returns><c>true</c> if the node is currently doing IBD, <c>false</c> otherwise.</returns>
        bool IsInitialBlockDownload();
    }
}
