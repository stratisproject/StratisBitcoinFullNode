using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class LightWalletBroadcasterManager : BroadcasterManagerBase
    {
        private TimeSpan broadcastMaxTime = TimeSpan.FromSeconds(21);

        public LightWalletBroadcasterManager(IConnectionManager connectionManager, IWalletManager walletManager) : base(connectionManager, walletManager)
        {
        }

        /// <inheritdoc />
        public override void BroadcastTransaction(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            if (this.IsPropagated(transaction))
                return;

            this.PropagateTransactionToPeers(transaction, true);
        }
    }
}
