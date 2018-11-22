using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// This component is responsible retrieving signed multisig transactions (from <see cref="ICrossChainTransferStore"/>)
    /// and broadcasting them into the network.
    /// </summary>
    public interface ISignedMultisigTransactionBroadcaster
    {
        /// <summary>
        /// Broadcast signed transactions that are not in the mempool.
        /// </summary>
        /// <param name="leaderProvider">
        /// The current federated leader.
        /// </param>
        /// <remarks>
        /// The current federated leader equal the <see cref="IFederationGatewaySettings.PublicKey"/> before it can broadcast the transactions.
        /// </remarks>
        Task BroadcastTransactionsAsync(ILeaderProvider leaderProvider);
    }
}
