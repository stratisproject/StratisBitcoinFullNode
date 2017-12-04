using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class LightWalletBroadcastManager : BroadcastManagerBase
    {
        private readonly TimeSpan broadcastMaxTime = TimeSpan.FromSeconds(25);

        public LightWalletBroadcastManager(IConnectionManager connectionManager) : base(connectionManager)
        {
        }

        /// <inheritdoc />
        public override async Task<bool> TryBroadcastAsync(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            if (IsPropagated(transaction))
                return true;

            this.PropagateTransactionToPeers(transaction, true);

            var elapsed = TimeSpan.Zero;
            var checkFrequency = TimeSpan.FromSeconds(1);

            while (elapsed < this.broadcastMaxTime)
            {
                var transactionEntry = this.GetTransaction(transaction.GetHash());
                if (transactionEntry != null && transactionEntry.State == State.Propagated)
                    return true;

                await Task.Delay(checkFrequency).ConfigureAwait(false);
                elapsed += checkFrequency;
            }

            return false;
        }
    }
}
