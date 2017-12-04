using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class FullNodeBroadcasterManager : BroadcasterManagerBase
    {
        /// <summary>Memory pool validator for validating transactions.</summary>
        private readonly IMempoolValidator mempoolValidator;

        public FullNodeBroadcasterManager(IConnectionManager connectionManager, IMempoolValidator mempoolValidator) : base(connectionManager)
        {
            Guard.NotNull(mempoolValidator, nameof(mempoolValidator));

            this.mempoolValidator = mempoolValidator;
        }

        /// <inheritdoc />
        public override async Task<bool> TryBroadcastAsync(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            if (this.IsPropagated(transaction))
                return true;

            var state = new MempoolValidationState(false);
            if (!await this.mempoolValidator.AcceptToMemoryPool(state, transaction).ConfigureAwait(false))
            {
                this.AddOrUpdate(transaction, State.CantBroadcast);
                return false;
            }

            this.PropagateTransactionToPeers(transaction);

            return true;
        }
    }
}
