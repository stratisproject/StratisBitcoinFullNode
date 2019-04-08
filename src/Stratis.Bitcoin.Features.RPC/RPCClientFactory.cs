using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC
{
    /// <summary>
    /// An interface for a factory that can create <see cref="IRPCClient"/> instances.
    /// </summary>
    public interface IRPCClientFactory
    {
        /// <summary>
        /// Create a new RPCClient instance.
        /// </summary>
        /// <param name="rpcSettings">The RPC settings.</param>
        /// <param name="address">The binding address.</param>
        /// <param name="network">The network.</param>
        IRPCClient Create(RpcSettings rpcSettings, Uri address, Network network);
    }

    /// <summary>
    /// A factory for creating new instances of an <see cref="RPCClient"/>.
    /// </summary>
    public class RPCClientFactory : IRPCClientFactory
    {
        /// <inheritdoc/>
        public IRPCClient Create(RpcSettings rpcSettings, Uri address, Network network)
        {
            Guard.NotNull(address, nameof(address));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(rpcSettings, nameof(rpcSettings));

            return new RPCClient(rpcSettings, address.ToString(), network);
        }
    }
}
