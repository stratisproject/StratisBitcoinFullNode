using System;
using Stratis.Bitcoin.Common;
using Stratis.Bitcoin.Common.Hosting;

namespace Stratis.Bitcoin.Builder
{
    /// <summary>
    /// Contract for the full node built by full node builder.
    /// </summary>
    public interface IFullNode : IDisposable
    {
        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        INodeLifetime NodeLifetime { get; }
        
        /// <summary>Access to DI services and features registered for the full node.</summary>
        IFullNodeServiceProvider Services { get; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        NBitcoin.Network Network { get; }

        /// <summary>Software version of the full node.</summary>
        Version Version { get; }

        /// <summary>
        /// Starts the full node and all its features.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the full node and all its features.
        /// </summary>
        void Stop();
    }
}