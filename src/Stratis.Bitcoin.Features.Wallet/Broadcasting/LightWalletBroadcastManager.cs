using System;
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
        private readonly TimeSpan broadcastFrequencySec = TimeSpan.FromSeconds(2);
        private readonly TimeSpan broadcastMaxTime = TimeSpan.FromSeconds(660);

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

            var elapsed = TimeSpan.Zero;
            var frequency = (int)this.broadcastFrequencySec.TotalMilliseconds;

            while (elapsed < this.broadcastMaxTime)
            {
                this.PropagateTransactionToPeers(transaction);

                var transactionEntry = this.GetTransaction(transaction.GetHash());
                if (transactionEntry != null && transactionEntry.State == Bitcoin.Broadcasting.State.Propagated)
                {
                    return true;
                }

                await Task.Delay(frequency).ConfigureAwait(false);
                elapsed += this.broadcastFrequencySec;
            }


            //this.PropagateTransactionToPeers(transaction);

            /*

            if (result == Bitcoin.Broadcasting.Success.DontKnow)
            {
                // wait for propagation
                var waited = TimeSpan.Zero;
                var period = TimeSpan.FromSeconds(1);
                while (TimeSpan.FromSeconds(21) > waited)
                {
                    // if broadcasts doesn't contain then success
                    var transactionEntry = this.broadcastManager.GetTransaction(transaction.GetHash());
                    if (transactionEntry != null && transactionEntry.State == Bitcoin.Broadcasting.State.Propagated)
                    {
                        return this.Json(model);
                    }
                    await Task.Delay(period).ConfigureAwait(false);
                    waited += period;
                }
            }

            */

            //TODO
            //return Success.DontKnow;

            return false;
        }
    }
}
