using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public class ChainIndexerRangeQuery
    {
        private readonly ChainIndexer chainIndexer;

        public ChainIndexerRangeQuery(ChainIndexer chainIndexer)
        {
            this.chainIndexer = chainIndexer;
        }

        /// <summary>
        /// Enumerates a range of chained headers. Inclusive of headers at <see cref="fromBlockHeight"/> and <see cref="toBlockHeight"/>.
        /// If the chain tip is lower than <see cref="fromBlockHeight"/> or <see cref="fromBlockHeight"/> is lower than <see cref="toBlockHeight"/>, will return empty.
        /// </summary>
        /// <param name="fromBlockHeight">The block height to start at, inclusive.</param>
        /// <param name="toBlockHeight">The block height to end at, inclusive. If not specified, will enumerate all blocks until the chain tip.</param>
        /// <returns></returns>
        public IEnumerable<ChainedHeader> EnumerateRange(int fromBlockHeight, int? toBlockHeight)
        {
            if (toBlockHeight.HasValue && toBlockHeight.Value < fromBlockHeight)
                yield break;

            // Snapshot the tip here.
            ChainedHeader tip = this.chainIndexer.Tip;

            if (tip.Height < fromBlockHeight)
            {
                yield break;
            }

            ChainedHeader toHeader = toBlockHeight.HasValue && toBlockHeight <= tip.Height
                ? this.chainIndexer.GetHeader(toBlockHeight.Value)
                : tip;

            if (toHeader == null)
            {
                yield break;
            }

            // In order to 'snapshot' the chain, enumerate all headers in reverse order from the tip.
            var chain = new List<ChainedHeader>();

            ChainedHeader currentHeader = toHeader;

            while (currentHeader != null)
            {
                chain.Add(currentHeader);

                if (currentHeader.Height == fromBlockHeight)
                    break;

                currentHeader = currentHeader.Previous;
            }

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                yield return chain[i];
            }
        }
    }
}