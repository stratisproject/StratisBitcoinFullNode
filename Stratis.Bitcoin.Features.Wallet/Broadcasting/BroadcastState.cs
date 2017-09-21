using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class BroadcastState
    {
        /// <summary>Memory pool validator for validating transactions.</summary>
        private readonly IMempoolValidator validator;

        /// <summary>Connection manager for managing node connections.</summary>
        private readonly IConnectionManager connectionManager;
        
        public ConcurrentHashSet<Transaction> Broadcasts { get; }

        public BroadcastState(
            IMempoolValidator validator,
            IConnectionManager connectionManager
            )
        {
            this.validator = validator;
            this.connectionManager = connectionManager;
            this.Broadcasts = new ConcurrentHashSet<Transaction>();
        }

        public async Task<bool> TryBroadcastAsync(Transaction transaction, TimeSpan waitForPropagation)
        {
            Guard.NotNull(transaction, nameof(transaction));

            // in a fullnode implementation this will validate with the 
            // mempool and broadcast, in a lightnode this will push to 
            // the wallet and then broadcast (we might add some basic validation
            if (this.validator == null)
            {

            }
            else
            {
                var state = new MempoolValidationState(false);
                if (!this.validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult())
                    return false;
            }

            // ask half of the peers if they're interested in our transaction
            this.Broadcasts.Add(transaction);
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

            var waited = TimeSpan.Zero;
            var period = TimeSpan.FromSeconds(1);
            while (waitForPropagation > waited)
            {
                // if broadcasts doesn't contain then success
                if(this.Broadcasts.All(x=>x.GetHash() != transaction.GetHash()))
                {
                    return true;
                }
                await Task.Delay(period).ConfigureAwait(false);
                waited += period;
            }
            return false;
        }
    }
}
