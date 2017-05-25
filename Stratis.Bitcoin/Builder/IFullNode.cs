using System;
using Microsoft.AspNetCore.Hosting;

namespace Stratis.Bitcoin.Builder
{
    public interface IFullNode : IDisposable
    {
        IApplicationLifetime ApplicationLifetime { get; }
        IFullNodeServiceProvider Services { get; }
        NBitcoin.Network Network { get; }
        Version Version { get; }
        FullNode.CancellationProvider GlobalCancellation { get; }
        void Start();
        void Stop();
    }
}