using System;
using Microsoft.AspNetCore.Hosting;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin
{
    /// <summary>
    /// Contract for the full node built by full node builder.
    /// </summary>
    public interface IFullNode : IDisposable
    {
        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        INodeLifetime NodeLifetime { get; }

        /// <summary>Provider of date time functionality.</summary>
        IDateTimeProvider DateTimeProvider { get; }

        /// <summary>Access to DI services and features registered for the full node.</summary>
        IFullNodeServiceProvider Services { get; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        NBitcoin.Network Network { get; }

        /// <summary>Software version of the full node.</summary>
        Version Version { get; }

        /// <summary>ASP.NET Core host for RPC server.</summary>
        IWebHost RPCHost { get; set; }

        /// <summary>Provides current state of the node.</summary>
        FullNodeState State { get; }

        /// <summary>Time the node started.</summary>
        DateTime StartTime { get; }

        /// <summary>
        /// Starts the full node and all its features.
        /// </summary>
        void Start();

        /// <summary>
        /// Initializes DI services that the node needs.
        /// </summary>
        /// <param name="serviceProvider">Provider of DI services.</param>
        /// <returns>Full node itself to allow fluent code.</returns>
        IFullNode Initialize(IFullNodeServiceProvider serviceProvider);

        /// <summary>
        /// Find a service of a particular type
        /// </summary>
        /// <typeparam name="T">Class of type</typeparam>
        /// <param name="failWithDefault">Set to true to return null instead of throwing an error</param>
        /// <returns></returns>
        T NodeService<T>(bool failWithDefault = false);

        /// <summary>
        /// Find a feature of a particular type or having a given interface
        /// </summary>
        /// <typeparam name="T">Class of interface type</typeparam>
        /// <param name="failWithError">Set to false to return null instead of throwing an error</param>
        /// <returns></returns>
        T NodeFeature<T>(bool failWithError = false);
    }

    /// <summary>Represents <see cref="IFullNode"/> state.</summary>
    public enum FullNodeState
    {
        /// <summary>Assigned when <see cref="IFullNode"/> instance is created.</summary>
        Created,

        /// <summary>Assigned when <see cref="IFullNode.Initialize"/> is called.</summary>
        Initializing,

        /// <summary>Assigned when <see cref="IFullNode.Initialize"/> finished executing.</summary>
        Initialized,

        /// <summary>Assigned when <see cref="IFullNode.Start"/> is called.</summary>
        Starting,

        /// <summary>Assigned when <see cref="IFullNode.Start"/> finished executing.</summary>
        Started,

        /// <summary>Assigned when <see cref="IFullNode.Dispose"/> is called.</summary>
        Disposing,

        /// <summary>Assigned when <see cref="IFullNode.Dispose"/> finished executing.</summary>
        Disposed
    }
}
