using NBitcoin;
using Stratis.Bitcoin.Utilities;

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
            Guard.NotNull(pubKey, nameof(pubKey));

            this.PubKey = pubKey;
        }

        /// <inheritdoc />
        public PubKey PubKey { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.PubKey)}:'{this.PubKey.ToHex()}'";
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var item = obj as FederationMember;
            if (item == null)
                return false;

            return this.PubKey.Equals(item.PubKey);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PubKey.GetHashCode();
        }

        public static bool operator ==(FederationMember a, FederationMember b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return a.PubKey == b.PubKey;
        }

        public static bool operator !=(FederationMember a, FederationMember b)
        {
            return !(a == b);
        }
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

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var item = obj as CollateralFederationMember;
            if (item == null)
                return false;

            return this.PubKey.Equals(item.PubKey);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PubKey.GetHashCode();
        }

        public static bool operator ==(CollateralFederationMember a, CollateralFederationMember b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return (a.PubKey == b.PubKey) && (a.CollateralAmount == b.CollateralAmount) && (a.CollateralMainchainAddress == b.CollateralMainchainAddress);
        }

        public static bool operator !=(CollateralFederationMember a, CollateralFederationMember b)
        {
            return !(a == b);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return base.ToString() + $",{nameof(this.CollateralAmount)}:{this.CollateralAmount},{nameof(this.CollateralMainchainAddress)}:{this.CollateralMainchainAddress ?? "null"}";
        }
    }
}
