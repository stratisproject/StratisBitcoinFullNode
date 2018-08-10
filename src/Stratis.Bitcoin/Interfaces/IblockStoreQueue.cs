using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IblockStoreQueue : IBlockStore
    {
        /// <summary>Adds a block to the saving queue.</summary>
        /// <param name="chainedHeaderBlock">The block and its chained header pair to be added to pending storage.</param>
        void AddToPending(ChainedHeaderBlock chainedHeaderBlock);

        /// <summary>Shows the stats to the console.</summary>
        void ShowStats(StringBuilder benchLog);
    }
}
