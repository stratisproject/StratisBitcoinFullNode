using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>
    /// This class determines which federated member to select as the next leader based on a change in block height.
    /// </summary>
    public interface ILeaderProvider
    {
        /// <summary>Public key of the current leader.</summary>
        PubKey CurrentLeaderKey { get; }

        void Update(BlockTipModel blockTipModel);
    }

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

        public LeaderProvider(IFederationGatewaySettings federationGatewaySettings)
        {
            this.orderedFederationPublicKeys = federationGatewaySettings.FederationPublicKeys.
                Select(k => k.ToString()).
                OrderBy(j => j).
                ToList().
                AsReadOnly();

            this.CurrentLeaderKey = new PubKey(this.orderedFederationPublicKeys.First());
        }

        public PubKey CurrentLeaderKey { get; private set; }

        public void Update(BlockTipModel blockTipModel)
        {
            Guard.NotNull(blockTipModel, nameof(blockTipModel));

            this.CurrentLeaderKey = new PubKey(this.orderedFederationPublicKeys[blockTipModel.Height % this.orderedFederationPublicKeys.Count]);
        }
    }
}
