using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class LightWalletBroadcastManager : BroadcastManagerBase
    {
        public LightWalletBroadcastManager(IConnectionManager connectionManager) : base(connectionManager)
        {
        }

        /// <inheritdoc />
        public override async Task<Success> TryBroadcastAsync(Transaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            if (IsPropagated(transaction))
                return Success.Yes;

            this.PropagateTransactionToPeers(transaction);

            return Success.DontKnow;
        }
    }
}
