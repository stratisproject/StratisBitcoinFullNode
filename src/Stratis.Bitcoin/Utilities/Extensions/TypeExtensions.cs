using System;
using System.Security;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Converts a long that represents a number of bytes to be represented in MB.
        /// </summary>
        public static decimal BytesToMegaBytes(this long input, int decimals = 2)
        {
            decimal result = Convert.ToDecimal(input / Math.Pow(2, 20));
            return Math.Round(result, decimals);
        }
    }
}
