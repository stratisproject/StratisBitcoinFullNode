using System;
using System.Collections.Generic;
using System.Text;

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

        /// <summary>
        /// Sets last IBD status update time and result.
        /// <para>Used in tests only.</para>
        /// </summary>
        /// <param name="blockDownloadState">New value for the IBD status, <c>true</c> means the node is considered in IBD.</param>
        /// <param name="lockStateUntil">Time until IBD state won't be changed.</param>
        void SetIsInitialBlockDownload(bool blockDownloadState, DateTime lockStateUntil);
    }
}
