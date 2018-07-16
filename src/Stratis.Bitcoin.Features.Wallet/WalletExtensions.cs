﻿using System;
using System.Linq;
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
            return chain.Tip.Header.BlockTime.ToUnixTimeSeconds() > (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - TimeSpan.FromHours(1).TotalSeconds);
        }

        /// <summary>
        /// Gets the height of the first block created at or after this date.
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
                DateTime blockTimeAtCheck = chain.GetBlock(check).Header.BlockTime.DateTime;

                if (blockTimeAtCheck > date)
                {
                    upperLimit = check;
                }
                else if (blockTimeAtCheck < date)
                {
                    lowerLimit = check;
                }
                else
                {
                    return check;
                }

                if (upperLimit - lowerLimit <= 1)
                {
                    blockSyncStart = upperLimit;
                    found = true;
                }
            }

            return blockSyncStart;
        }

        // TODO Remove this code duplication
        public enum WalletScOpcodeType : byte
        {
            // smart contracts
            OP_CREATECONTRACT = 0xc0,
            OP_CALLCONTRACT = 0xc1
        }

        public static bool IsSmartContractExec(this Script script)
        {
            Op firstOp = script.ToOps().FirstOrDefault();

            if (firstOp == null)
                return false;

            var opCode = (byte)firstOp.Code;

            return opCode == (byte)WalletScOpcodeType.OP_CALLCONTRACT || opCode == (byte)WalletScOpcodeType.OP_CREATECONTRACT;
        }
    }
}
