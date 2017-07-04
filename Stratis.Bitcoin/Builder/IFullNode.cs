using System;
using Stratis.Bitcoin.Common;
using Stratis.Bitcoin.Common.Hosting;

namespace Stratis.Bitcoin.Builder
{
    public interface IFullNode : IDisposable
    {
        INodeLifetime NodeLifetime { get; }
        IFullNodeServiceProvider Services { get; }
        NBitcoin.Network Network { get; }
        Version Version { get; }
        void Start();
        void Stop();
    }
}