using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="ChainedHeader"/>.
    /// </summary>
    [DebuggerStepThrough()]
    public static class ChainedHeaderExtentions
    {
        /// <summary>
        /// Convert two chained headers on the same chain to a list of consecutive items. 
        /// </summary>
        /// <param name="topHeader">The at the top.</param>
        /// <param name="bottomHeader">The header at the bottom.</param>
        /// <returns>List of consecutive items.</returns>
        /// <exception cref="InvalidOperationException">Then the items are not on the same chain.</exception>
        public static List<ChainedHeader> ToConsecutiveList(this ChainedHeader topHeader, ChainedHeader bottomHeader)
        {
            var downloadList = new List<ChainedHeader>();
            ChainedHeader current = topHeader;
            while (current != bottomHeader)
            {
                if(current == null)
                    throw new InvalidOperationException("Headers not on the same chain.");

                downloadList.Add(current);
                current = current.Previous;
            }

            return downloadList;
        }
    }
}
