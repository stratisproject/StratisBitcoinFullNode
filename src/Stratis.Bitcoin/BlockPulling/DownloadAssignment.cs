using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Describes the assigned download tasks.
    /// </summary>
    public class DownloadAssignment
    {
        /// <summary>Hash of the block being downloaded.</summary>
        public uint256 BlockHash { get; set; }

        /// <summary>Stopwatch used to measure the time the responsible peer needed to provide the block.</summary>
        public Stopwatch Stopwatch { get; set; }

        /// <summary>
        /// Initializes a new instance of the object and possibly starts the internal watch.
        /// </summary>
        /// <param name="blockHash">Hash of the block being downloaded.</param>
        /// <param name="start">If <c>true</c>, the download task's stopwatch will be started immediately.</param>
        public DownloadAssignment(uint256 blockHash, bool start = true)
        {
            this.BlockHash = blockHash;
            this.Stopwatch = new Stopwatch();
            if (start) this.Stopwatch.Start();
        }

        /// <summary>
        /// Stops the task's stopwatch and returns elapsed time.
        /// </summary>
        /// <returns>Number of milliseconds since the task started.</returns>
        public long Finish()
        {
            this.Stopwatch.Stop();
            return this.Stopwatch.ElapsedMilliseconds;
        }
    }
}
