using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// A request to the block puller that holds the chained headers of the blocks that are requested for download.
    /// </summary>
    internal class BlockDownloadRequest
    {
        /// <summary>The list of block headers to download.</summary>
        public List<ChainedHeader> BlocksToDownload { get; set; }
    }
}