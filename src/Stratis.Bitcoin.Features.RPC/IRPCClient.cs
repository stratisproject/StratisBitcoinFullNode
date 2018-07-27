using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.RPC
{
    /// <summary>
    /// Interface for an RPC client.
    /// </summary>
    /// <remarks>Extend where necessary.</remarks>
    /// <see cref="https://en.bitcoin.it/wiki/Original_Bitcoin_client/API_calls_list"/>
    public interface IRPCClient
    {
        /// <summary>Send a command over RPC.</summary>
        /// <param name="commandName">The command to execute.</param>
        /// <param name="parameters">An array of command parameters.</param>
        /// <returns>The RPC response.</returns>
        RPCResponse SendCommand(RPCOperations commandName, params object[] parameters);

        /// <summary>Send a command over RPC.</summary>
        /// <param name="request">The rpc request to execute.</param>
        /// <param name="throwIfRPCError">A value indicating whether an exception should be thrown when there is an RPC error.</param>
        /// <returns>The RPC response.</returns>
        RPCResponse SendCommand(RPCRequest request, bool throwIfRPCError = true);

        /// <summary>Send a command over RPC.</summary>
        /// <param name="commandName">The command to execute.</param>
        /// <param name="parameters">An array of command parameters.</param>
        /// <returns>The RPC response.</returns>
        RPCResponse SendCommand(string commandName, params object[] parameters);

        /// <summary>Send a command over RPC.</summary>
        /// <param name="commandName">The command to execute.</param>
        /// <param name="parameters">An array of command parameters.</param>
        /// <returns>The RPC response.</returns>
        Task<RPCResponse> SendCommandAsync(RPCOperations commandName, params object[] parameters);

        /// <summary>Send a command over RPC.</summary>
        /// <param name="request">The rpc request to execute.</param>
        /// <param name="throwIfRPCError">A value indicating whether an exception should be thrown when there is an RPC error.</param>
        /// <returns>The RPC response.</returns>
        Task<RPCResponse> SendCommandAsync(RPCRequest request, bool throwIfRPCError = true);

        /// <summary>Send a command over RPC.</summary>
        /// <param name="commandName">The command to execute.</param>
        /// <param name="parameters">An array of command parameters.</param>
        /// <returns>The RPC response.</returns>
        Task<RPCResponse> SendCommandAsync(string commandName, params object[] parameters);
    }
}
