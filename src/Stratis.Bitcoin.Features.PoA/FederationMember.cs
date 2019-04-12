using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <summary>Interface that contains data that defines a federation member.</summary>
    public interface IFederationMember
    {
        /// <summary>Public key of a federation member.</summary>
        PubKey PubKey { get; }
    }

    /// <summary>Representation of a federation member on standard PoA network.</summary>
    public class FederationMember : IFederationMember
    {
        public FederationMember(PubKey pubKey)
        {
            this.PubKey = pubKey;
        }

        /// <inheritdoc />
        public PubKey PubKey { get; }
    }

    /// <summary>Class that contains data that defines a federation member on federated peg sidechain.</summary>
    public class CollateralFederationMember : FederationMember
    {
        public CollateralFederationMember(PubKey pubKey, Money collateralAmount, string collateralMainchainAddress) : base(pubKey)
        {
            this.CollateralAmount = collateralAmount;
            this.CollateralMainchainAddress = collateralMainchainAddress;
        }

        /// <summary>Amount that federation member has to have on mainchain.</summary>
        public Money CollateralAmount { get; set; }

        /// <summary>Mainchain address that should have the collateral.</summary>
        public string CollateralMainchainAddress { get; set; }
    }
}
