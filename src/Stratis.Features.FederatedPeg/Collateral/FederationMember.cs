using NBitcoin;

namespace Stratis.Features.FederatedPeg.Collateral
{
    /// <summary>Class that contains data that defines a federation member on federated peg sidechain.</summary>
    public class FederationMember
    {
        /// <summary>Public key of a federation member.</summary>
        public PubKey PubKey { get; set; }

        /// <summary>Amount that federation member has to have on mainchain.</summary>
        public Money CollateralAmount { get; set; }

        /// <summary>Mainchain address that should have the collateral.</summary>
        public string CollateralMainchainAddress { get; set; }
    }
}
