using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// This class determines which federated member to select as the next leader based on a change in block height.
    /// <para>
    /// Each federated member is selected in a round robin fashion.
    /// </para>
    /// <remarks>
    /// On construction the provider will order the federated members' public keys - which live in <see cref="IFederationGatewaySettings"/> - before it determines the next leader.
    /// </remarks>
    /// </summary>
    public class LeaderProvider : ILeaderProvider
    {
        /// <summary>
        /// Ordered list of federated members' public keys.
        /// </summary>
        private readonly IReadOnlyList<string> orderedFederationPublicKeys;

        private PubKey currentLeader;

        public LeaderProvider(IFederationGatewaySettings federationGatewaySettings)
        {
            this.orderedFederationPublicKeys = federationGatewaySettings.FederationPublicKeys.
                Select(k => k.ToString()).
                OrderBy(j => j).
                ToList().
                AsReadOnly();
        }

        public PubKey CurrentLeader => this.currentLeader;

        public void Update(BlockTipModel blockTipModel)
        {
            Guard.NotNull(blockTipModel, nameof(blockTipModel));

            this.currentLeader = new PubKey(this.orderedFederationPublicKeys[blockTipModel.Height % this.orderedFederationPublicKeys.Count]);
        }
    }
}
