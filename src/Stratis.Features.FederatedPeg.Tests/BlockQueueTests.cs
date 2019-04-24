using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class BlockQueueTests
    {
        [Fact]
        public void Enqueue_Block_Increases_QueueSize()
        {
            var logger = Substitute.For<ILogger>();
            var asyncProvider = Substitute.For<IAsyncProvider>();
            var blockQueue = new BlockQueueProcessor(logger, asyncProvider, (_, token) => Task.CompletedTask);

            var block = new Block();

            block.ToBytes();

            Assert.True(block.BlockSize.HasValue);

            var size = block.BlockSize.Value;


            Assert.True(blockQueue.TryEnqueue(block));

            Assert.Equal(size, blockQueue.QueueSizeBytes);
        }

        [Fact]
        public void Dequeue_Block_Decreases_QueueSize()
        {
            var logger = Substitute.For<ILogger>();
            var asyncProvider = Substitute.For<IAsyncProvider>();
            Func<Block, CancellationToken, Task> callback = (_, token) => Task.CompletedTask;

            Func<Block, CancellationToken, Task> blockQueueCallback = null;

            // Intercept the block queue callback being provided to the queuer.
            // We do this so it can be manually invoked later.
            asyncProvider
                .When(provider => provider.CreateAndRunAsyncDelegateDequeuer(Arg.Any<string>(), Arg.Any<Func<Block, CancellationToken, Task>>()))
                .Do(info =>
                {
                    blockQueueCallback = info.ArgAt<Func<Block, CancellationToken, Task>>(1);
                });

            var blockQueue = new BlockQueueProcessor(logger, asyncProvider, callback);

            var block = new Block();

            block.ToBytes();

            Assert.True(block.BlockSize.HasValue);

            long size = block.BlockSize.Value;

            Assert.True(blockQueue.TryEnqueue(block));

            Assert.Equal(size, blockQueue.QueueSizeBytes);

            // Invoke the block queue callback and test that it reduces the queue size.
            blockQueueCallback(block, CancellationToken.None);

            Assert.Equal(0, blockQueue.QueueSizeBytes);
        }
    }
}