using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
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

            ChainedHeader fromHeader = this.chainIndexer.GetHeader(fromBlockHeight);

            if (fromHeader == null)
            {
                yield break;
            }

            // Return the start header.
            yield return fromHeader;

            IEnumerable<ChainedHeader> blockHeaders = this.chainIndexer.EnumerateAfter(fromHeader);

            if (toBlockHeight != null)
            {
                ChainedHeader toHeader = this.chainIndexer.GetHeader(toBlockHeight.Value);

                if (toHeader == fromHeader)
                {
                    // We're done.
                    yield break;
                }

                // If we have a toHeader and it's not the same as the start header.
                if (toHeader != null)
                {
                    blockHeaders = blockHeaders.TakeWhile(b => b != toHeader).Append(toHeader);
                }
            }

            foreach (ChainedHeader blockHeader in blockHeaders)
            {
                yield return blockHeader;
            }
        }
    }
}