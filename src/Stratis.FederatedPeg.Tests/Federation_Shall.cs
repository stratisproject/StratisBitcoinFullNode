using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.FederatedPeg.Features.FederationGateway.Federation;

namespace Stratis.FederatedPeg.Tests
{
    [Collection("FederatedPegTests")]
    public class Federation_Shall
    {
        [Fact]
        public void create_a_federation()
        {
            var federation = new Federation(2, 3, this.GetSampleMembers());
            federation.M.Should().Be(2);
            federation.N.Should().Be(3);
            federation.Members.Count.Should().Be(3);
            federation.Members.Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void generate_addresses()
        {
            var federation = new Federation(2, 3, this.GetSampleMembers());
            federation.GetPublicKeys(Chain.Mainchain).Length.Should().Be(3);
            federation.GetPublicKeys(Chain.Mainchain).Should().OnlyHaveUniqueItems();
            federation.GetPublicKeys(Chain.Sidechain).Length.Should().Be(3);
            federation.GetPublicKeys(Chain.Sidechain).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void generate_scriptpubkey()
        {
            var federation = new Federation(2, 3, this.GetSampleMembers());
            federation.GenerateScriptPubkey(Chain.Mainchain).ToHex().Length.Should().BeGreaterThan(160);
        }

        [Fact]
        public void give_correct_payment_script()
        {
            var publicKey1 = new PubKey("0374860560f816100ee4917af0bfb416d559cfcbb587e539e55f09e22c8cf07c9a");
            var publicKey2 = new PubKey("0226874aa4fb722ed3294c7d56f5d9dfdbaa0f79aaebf6f2eb925dc3bd0d7b5458");
            var publicKey3 = new PubKey("0390da9f7a8bdcc3a9e2a6ad89349fb86599c65d92b36b00e0b175cc3ad10fda2e");

            var script = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(3,
                new[] {publicKey1,
                    publicKey2,
                    publicKey3
                });

            var paymentScipt = script.PaymentScript;

            var address = script.Hash.GetAddress(Network.StratisRegTest);
            var paymentScript2 = address.ScriptPubKey;
            paymentScipt.Should().BeEquivalentTo(paymentScript2);

            var bitcoinAddress = BitcoinAddress.Create(address.ToString(), Network.StratisRegTest);
            var paymentScript3 = bitcoinAddress.ScriptPubKey;
            paymentScript2.Should().BeEquivalentTo(paymentScript3);
        }

        private IEnumerable<FederationMember> GetSampleMembers()
        {
            var member1 = FederationMemberPrivate.CreateNew("John Smith", "pass1").ToFederationMember();
            var member2 = FederationMemberPrivate.CreateNew("Ivan Draco", "pass2").ToFederationMember();
            var member3 = FederationMemberPrivate.CreateNew("Elizabeth", "pass3").ToFederationMember();
            return new[] {member1, member2, member3};
        }
    }
}
