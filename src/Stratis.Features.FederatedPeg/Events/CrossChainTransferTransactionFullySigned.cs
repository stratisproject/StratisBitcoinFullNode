using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.Events
{
    /// <summary>
    /// Raised when the partial crosschain transactions of a deposit are merged together and the final transaction is fully signed.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class CrossChainTransferTransactionFullySigned : EventBase
    {
        public ICrossChainTransfer Transfer { get; }

        public CrossChainTransferTransactionFullySigned(ICrossChainTransfer transfer)
        {
            this.Transfer = transfer;
        }
    }
}
