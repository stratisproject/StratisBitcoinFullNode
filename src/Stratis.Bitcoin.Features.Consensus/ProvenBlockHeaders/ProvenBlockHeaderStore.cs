using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStore : IProvenBlockHeaderStore, IDisposable
    {
        // TODO

        // Add Database key under which the ProvebBlockHeader current tip is stored

        // Add Instance logger

        // Access to DBreeze database

        // Ad Specification of the network the node runs on - regtest/testnet/mainnet

        // A Hash of the block which is currently the tip of the chain

        // Add performance counter

        public Task InitializeAsync()
        {
            throw new NotImplementedException();
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Pop some items to remove a window of 10 % of the threshold.
            throw new NotImplementedException();
        }

        public Task<ProvenBlockHeader> GetAsync(uint256 blockId, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<ProvenBlockHeader> GetTipAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task SetAsync(ProvenBlockHeader provenBlockHeader, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

    }
}
