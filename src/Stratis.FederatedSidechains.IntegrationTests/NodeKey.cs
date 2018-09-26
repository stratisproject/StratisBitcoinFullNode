using System.IO;
using Stratis.FederatedPeg;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedSidechains.IntegrationTests
{
    public enum NodeRole
    {
        Wallet,
        Federation,
    }

    public struct FederationMemberKey
    {
        public NodeRole Role => NodeRole.Federation;
        public int Index;

        public string Name => NamingConstants.CrossChain + Role + Index;
        public string Password => NamingConstants.Password + Name;
        public string MultisigWalletName => NamingConstants.Multisig + NamingConstants.Wallet;

    }
    
    public struct NodeKey
    {
        public Chain Chain;
        public NodeRole Role;
        public int Index;
        public int SelfApiPort => (Chain == Chain.Mainchain ? 101 : 102) * 100 + Index;
        public int CounterChainApiPort => (Chain == Chain.Mainchain ? 102 : 101) * 100 + Index;

        public string Name => Chain.ToString() + Role + Index;
        public string Password => nameof(Password) + Name;
        public string Passphrase => nameof(Passphrase) + Name;
        public string WalletName => NamingConstants.Wallet + Name;

        public FederationMemberKey AsFederationMemberKey()
        {
            return new FederationMemberKey() {Index = this.Index};
        }
    }
}