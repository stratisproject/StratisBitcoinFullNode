using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    /// <summary>
    /// This interface provides the System.Threading.Tasks.Dataflow producer and consumer implementation
    /// to queue up <see cref="Block"/>s, which are broadcast from the <see cref="ConsensusLoop"/>, and then synchronised within <see cref="WalletManager"/>.
    /// </summary>
    public interface IWalletBlockProducerConsumer
    {
        /// <summary>
        /// Queue a <see cref="Block"/> that is broadcast from the <see cref="ConsensusLoop"/>.
        /// </summary>
        /// <param name="block">The block that was broadcast out from Consensus</param>.
        void QueueBlock(Block block);

        /// <summary>
        /// Produce the Dataflow block that is a target for the data (NBitcoin <see cref="Block"/>).
        /// </summary>
        /// <param name="target">Dataflow Task TargetBlock</param>
        /// <param name="block">NBitcoin <see cref="Block"/></param>
        void Produce(ITargetBlock<Block> target, Block block);

        /// <summary>
        /// Consumes the Dataflow block which receives NBitcoin <see cref="Block"/>s.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="queue"></param>
        Task ConsumeAsync(ISourceBlock<Block> source, ConcurrentQueue<Block> queue);

        /// <summary>
        /// Provides a Buffer for storing NBitcoin <see cref="Block"/>s. 
        /// This serves as the target buffer for the producer and the source buffer for the consumer.
        /// </summary>
        BufferBlock<Block> BlockBuffer { get; }
    }
}
