using System.Threading;

namespace Stratis.Bitcoin.Features.Miner.PosMinting
{
    public partial class PosMinting
    {
        /// <summary>
        /// Result of a task of coinstake worker that looks for kernel.
        /// </summary>
        public partial class CoinstakeWorkerResult
        {
            /// <summary>Invalid worker index as a sign that kernel was not found.</summary>
            public const int KernelNotFound = -1;

            /// <summary>Index of the worker that found the index, or <see cref="KernelNotFound"/> if no one found the kernel (yet).</summary>
            private int kernelFoundIndex;

            /// <summary>Index of the worker that found the index, or <see cref="KernelNotFound"/> if no one found the kernel (yet).</summary>
            public int KernelFoundIndex => this.kernelFoundIndex;

            /// <summary>UTXO that satisfied the target difficulty.</summary>
            public UtxoStakeDescription KernelCoin { get; set; }

            /// <summary>
            /// Initializes an instance of the object.
            /// </summary>
            public CoinstakeWorkerResult()
            {
                this.kernelFoundIndex = KernelNotFound;
                this.KernelCoin = null;
            }

            /// <summary>
            /// Sets the founder of the kernel in thread-safe manner.
            /// </summary>
            /// <param name="workerIndex">Worker's index to set as the founder of the kernel.</param>
            /// <returns><c>true</c> if the worker's index was set as the kernel founder, <c>false</c> if another worker index was set earlier.</returns>
            public bool SetKernelFoundIndex(int workerIndex)
            {
                return Interlocked.CompareExchange(ref this.kernelFoundIndex, workerIndex, KernelNotFound)
                       == KernelNotFound;
            }
        }
    }
}