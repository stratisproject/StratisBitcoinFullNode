﻿using Stratis.Bitcoin.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using System.Threading.Tasks;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class LightWalletBroadcasterManager : BroadcasterManagerBase
    {
        /// <summary>Connection manager for managing node connections.</summary>
        private readonly IConnectionManager connectionManager;

        public LightWalletBroadcasterManager(IConnectionManager connectionManager) : base()
        {
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        /// <inheritdoc />
        public override async Task<Success> TryBroadcastAsync(Transaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            var found = GetTransaction(transaction.GetHash());
            if (found != null)
            {
                if (found.State == State.Propagated) return Success.Yes;
                if (found.State == State.CantBroadcast)
                {
                    AddOrUpdate(transaction, State.ToBroadcast);
                }
            }
            else
            {
                AddOrUpdate(transaction, State.ToBroadcast);
            }

            // ask half of the peers if they're interested in our transaction
            var invPayload = new InvPayload(transaction);
            var oneTwo = 1;
            foreach (var node in this.connectionManager.ConnectedNodes)
            {
                if (oneTwo == 1)
                {
                    await node.SendMessageAsync(invPayload).ConfigureAwait(false);
                }
                oneTwo = oneTwo == 1 ? 2 : 1;
            }
            return Success.DontKnow;
        }
    }
}