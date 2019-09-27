using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository.Extensions
{
    public static class ChainedHeaderExt
    {
        /// <summary>
        /// Heights counterpart for <see cref="ChainedHeader.GetLocator"/>.
        /// </summary>
        /// <param name="tipHeight">The tip height used to calculate locator heights.</param>
        /// <returns>Returns a list of heights corresponding to the blocks of a locator created at the given height.</returns>
        public static List<int> GetLocatorHeights(int? tipHeight)
        {
            int nStep = 1;
            var blockHeights = new List<int>();

            while (tipHeight != null)
            {
                blockHeights.Add((int)tipHeight);

                // Stop when we have added the genesis block.
                if (tipHeight == 0)
                    break;

                // Exponentially larger steps back, plus the genesis block.
                tipHeight = Math.Max((int)tipHeight - nStep, 0);

                if (blockHeights.Count > 10)
                    nStep *= 2;
            }

            return blockHeights;
        }
    }
}
