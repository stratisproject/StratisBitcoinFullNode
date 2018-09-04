using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Utilities
{
    public class NodeStats
    {
        /// <summary>Registers action that will be used to append node stats when they are being collected.</summary>
        /// <remarks>Node stats are usually single line stats that should display most important information.</remarks>
        /// <param name="appendStatsAction">Action that will be invoked during stats collection.</param>
        /// <param name="priority">Stats priority that will be used to determine invocation priority of stats collection.</param>
        public void RegisterNodeStats(Action<StringBuilder> appendStatsAction, int priority = 0)
        {

        }

        /// <summary>Registers action that will be used to append feature stats when they are being collected.</summary>
        /// <remarks>Feature stats are usually blocks of feature specific stats.</remarks>
        /// <param name="appendStatsAction">Action that will be invoked during stats collection.</param>
        /// <param name="priority">Stats priority that will be used to determine invocation priority of stats collection.</param>
        public void RegisterFeatureStats(Action<StringBuilder> appendStatsAction, int priority = 0)
        {

        }
    }
}
