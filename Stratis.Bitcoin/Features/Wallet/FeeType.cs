using System;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// An indicator on how fast a transaction will be accepted in a block
    /// </summary>
    public enum FeeType
    {
        /// <summary>
        /// Slow.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Avarage.
        /// </summary>
        Medium = 1,

        /// <summary>
        /// Fast.
        /// </summary>
        High = 105
    }

    public static class FeeParser
    {
        public static FeeType Parse(string value)
        {
            if (Enum.TryParse<FeeType>(value, true, out var ret))
                return ret;

            return FeeType.Medium;
        }

        /// <summary>
        /// Map a fee type to the number of confirmations
        /// </summary>
        public static int ToConfirmations(this FeeType fee)
        {
            switch (fee)
            {
                case FeeType.Low:
                    return 50;
                case FeeType.Medium:
                    return 20;
                case FeeType.High:
                    return 5;

            }

            throw new WalletException("Invalid fee");
        }
    }
}
