using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Builder
{
    public interface IFullNode : IDisposable
    {
        IFullNodeServiceProvider Services { get; }
        NBitcoin.Network Network { get; }
        System.Version Version { get; }

        void Start();
        Task RunAsync();
        Task RunAsync(CancellationToken cancellationToken, string shutdownMessage);
        void Run();
    }
}