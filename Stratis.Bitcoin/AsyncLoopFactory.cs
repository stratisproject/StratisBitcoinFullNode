using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
    public interface IAsyncLoopFactory
    {
        IAsyncLoop Create(string name, Func<CancellationToken, Task> loop);

        Task Run(string name, Func<CancellationToken, Task> loop, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);
        Task Run(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);
    }

    public class AsyncLoopFactory : IAsyncLoopFactory
    {
        public AsyncLoopFactory()
        {
        }

        public IAsyncLoop Create(string name, Func<CancellationToken, Task> loop)
        {
            return new AsyncLoop(name, loop);
        }

        public Task Run(string name, Func<CancellationToken, Task> loop, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            return new AsyncLoop(name, loop).Run(repeatEvery, startAfter);
        }

        public Task Run(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotNull(cancellation, nameof(cancellation));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));

            return new AsyncLoop(name, loop).Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(1000), startAfter);
        }
    }
}
