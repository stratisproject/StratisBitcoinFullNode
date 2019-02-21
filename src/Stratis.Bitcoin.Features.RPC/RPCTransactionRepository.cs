using System;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCTransactionRepository : ITransactionRepository
    {
        private RPCClient _Client;
        public RPCTransactionRepository(RPCClient client)
        {
            if(client == null)
                throw new ArgumentNullException("client");
            this._Client = client;
        }
#region ITransactionRepository Members

        public Task<Transaction> GetAsync(uint256 txId)
        {
            return this._Client.GetRawTransactionAsync(txId, null, false);
        }

        public Task BroadcastAsync(Transaction tx)
        {
            return this._Client.SendRawTransactionAsync(tx);
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return Task.FromResult(false);
        }

#endregion
    }
}
