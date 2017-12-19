#if !NOJSONNET
using System;
using System.Threading.Tasks;
using NBitcoin.RPC;

namespace NBitcoin
{
    public class RPCTransactionRepository : ITransactionRepository
    {
        RPCClient _Client;
        public RPCTransactionRepository(RPCClient client)
        {
            if(client == null)
                throw new ArgumentNullException("client");
            _Client = client;
        }
#region ITransactionRepository Members

        public Task<Transaction> GetAsync(uint256 txId)
        {
            return _Client.GetRawTransactionAsync(txId, false);
        }

        public Task BroadcastAsync(Transaction tx)
        {
            return _Client.SendRawTransactionAsync(tx);
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return Task.FromResult(false);
        }

#endregion
    }
}
#endif