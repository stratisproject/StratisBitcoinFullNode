﻿using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet
{
    public static class WalletExtensions
    {
        /// <summary>
        /// Determines whether the chain is downloaded and up to date.
        /// </summary>
        /// <param name="chain">The chain.</param>        
        public static bool IsDownloaded(this ConcurrentChain chain)
        {
            return chain.Tip.Header.BlockTime.ToUnixTimeSeconds() > (DateTimeOffset.Now.ToUnixTimeSeconds() - TimeSpan.FromHours(1).TotalSeconds);
        }

        /// <summary>
        /// Gets the height of the first block created after this date.
        /// </summary>
        /// <param name="chain">The chain of blocks.</param>
        /// <param name="date">The date.</param>
        /// <returns>The height of the first block created after the date.</returns>
        public static int GetHeightAtTime(this ConcurrentChain chain, DateTime date)
        {
            int blockSyncStart = 0;
            int upperLimit = chain.Tip.Height;
            int lowerLimit = 0;
            bool found = false;
            while (!found)
            {
                int check = lowerLimit + (upperLimit - lowerLimit) / 2;
                if (chain.GetBlock(check).Header.BlockTime >= date)
                {
                    upperLimit = check;
                }
                else if (chain.GetBlock(check).Header.BlockTime < date)
                {
                    lowerLimit = check;
                }

                if (upperLimit - lowerLimit <= 1)
                {
                    blockSyncStart = upperLimit;
                    found = true;
                }
            }

            return blockSyncStart;
        }
    }
}