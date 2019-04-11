using NBitcoin;

namespace Stratis.Features.FederatedPeg.Collateral
{
    /// <summary>Class that contains data that defines a federation member on federated peg sidechain.</summary>
    public class FederationMember
    {
        public FederationMember()
        {
        }

        public FederationMember(PubKey pubKey, Money collateralAmount, string collateralMainchainAddress)
        {
            this.PubKey = pubKey;
            this.CollateralAmount = collateralAmount;
            this.CollateralMainchainAddress = collateralMainchainAddress;
        }

        /// <summary>Public key of a federation member.</summary>
        public PubKey PubKey { get; set; }

        /// <summary>Amount that federation member has to have on mainchain.</summary>
        public Money CollateralAmount { get; set; }

        /// <summary>Mainchain address that should have the collateral or <c>null</c> if no collateral is required.</summary>
        public string CollateralMainchainAddress { get; set; }
    }
}
