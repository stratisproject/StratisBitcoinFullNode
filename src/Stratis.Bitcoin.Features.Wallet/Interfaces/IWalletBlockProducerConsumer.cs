using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    /// <summary>
    /// Interface for a manager providing producer and consumer operation
    /// blocks processed within wallets.
    /// </summary>
    public interface IWalletBlockProducerConsumer
    {
        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="block">The block.</param>
        void ProducerConsumerProcessBlock(Block block);

        /// <summary>
        /// Writes a list of NBitcoin Blocks to the System.Threading.Task.Dataflow.ITargetBlock object.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="block">The <see cref="Block>"/>.</param>
        void Produce(ITargetBlock<Block> target, Block block);

        /// <summary>
        /// Reads the list of NBitcoin Blocks against the System.Threading.Task.Dataflow.ISourceBlock object
        /// </summary>
        /// <param name="source"></param>
        Task ConsumeAsync(ISourceBlock<Block> source);

        /// <summary>
        /// Provides a Buffer that is a list NBitCoin Blocks. 
        /// This serves as the target buffer for the producer and the source buffer for the consumer.
        /// </summary>
        BufferBlock<Block> BlockBuffer { get; }
    }
}
