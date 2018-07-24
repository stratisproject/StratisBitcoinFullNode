using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.Miner.PosMinting
{
    public partial class PosMinting
    {
        /// <summary>
        /// Information needed by the coinstake worker for finding the kernel.
        /// </summary>
        public partial class CoinstakeWorkerContext
        {
            /// <summary>Worker's ID / index number.</summary>
            public int Index { get; set; }

            /// <summary>Logger with worker's prefix.</summary>
            public ILogger Logger { get; set; }

            /// <summary>List of UTXO descriptions that the worker should check.</summary>
            public List<UtxoStakeDescription> utxoStakeDescriptions { get; set; }

            /// <summary>Information related to coinstake transaction.</summary>
            public CoinstakeContext CoinstakeContext { get; set; }

            /// <summary>Result shared by all workers. A structure that determines the kernel founder and the kernel UTXO that satisfies the target difficulty.</summary>
            public CoinstakeWorkerResult Result { get; set; }
        }
    }
}