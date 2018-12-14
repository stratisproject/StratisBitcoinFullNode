using System.Collections.Generic;
using System.Threading.Tasks;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.RestClients;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public interface IMaturedBlocksRequester
    {
        /// <summary>
        /// Gets more blocks from the counter node.
        /// </summary>
        /// <returns><c>True</c> if more blocks were found and <c>false</c> otherwise.</returns>
        Task<bool> GetMoreBlocksAsync();
    }

    public class RestMaturedBlockRequester : IMaturedBlocksRequester
    {
        public const int MaxBlocksToCatchup = 1000;

        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly IMaturedBlockReceiver maturedBlockReceiver;
        private readonly IFederationGatewayClient federationGatewayClient;

        public RestMaturedBlockRequester(
            ICrossChainTransferStore crossChainTransferStore,
            IMaturedBlockReceiver maturedBlockReceiver,
            IFederationGatewayClient federationGatewayClient)
        {
            this.crossChainTransferStore = crossChainTransferStore;
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.federationGatewayClient = federationGatewayClient;
        }

        /// <inheritdoc />
        public async Task<bool> GetMoreBlocksAsync()
        {
            int maxBlocksToRequest = 1;

            if (!this.crossChainTransferStore.HasSuspended())
            {
                maxBlocksToRequest = MaxBlocksToCatchup;
            }

            var model = new MaturedBlockRequestModel(this.crossChainTransferStore.NextMatureDepositHeight, maxBlocksToRequest);

            List<IMaturedBlockDeposits> blockDeposits = await this.federationGatewayClient.GetMaturedBlockDepositsAsync(model).ConfigureAwait(false);

            if (blockDeposits != null)
            {
                if (blockDeposits.Count > 0)
                {
                    this.maturedBlockReceiver.PushMaturedBlockDeposits(blockDeposits.ToArray());

                    if (blockDeposits.Count < maxBlocksToRequest)
                    {
                        await this.crossChainTransferStore.SaveCurrentTipAsync().ConfigureAwait(false);
                    }
                }

                return true;
            }

            return false;
        }
    }
}
