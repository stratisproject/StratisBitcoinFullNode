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

        /// <summary>
        /// Starts the full node and all its features.
        /// </summary>
        void Start();

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
}
