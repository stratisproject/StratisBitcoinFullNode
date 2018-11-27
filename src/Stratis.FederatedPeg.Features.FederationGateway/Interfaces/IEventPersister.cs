using System.Threading.Tasks;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// This component is responsible for digesting the different events that happen on the target chain,
    /// and coordinate with the <see cref="ICrossChainTransferStore"/>> to persist the state changes that
    /// result from these events.
    /// </summary>
    public interface IEventPersister
    {
        /// <summary>
        /// When new matured block arrive, this will trigger a call to the store to persist the given deposits.
        /// </summary>
        Task PersistNewMaturedBlockDepositsAsync(IMaturedBlockDeposits[] maturedBlockDeposits);

        /// <summary>
        /// When a new block is produced on the source chain, this will persist the new tip in the store.
        /// </summary>
        Task PersistNewSourceChainTip(IBlockTip newTip);
    }
}