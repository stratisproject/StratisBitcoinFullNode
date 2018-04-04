
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.FederatedPeg.IntegrationTests.Helpers;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    [Collection("FederatedPegTests")]
    public class SidechainFunder_Can
    {
        [Fact]
        public async Task deposit_funds_to_sidechain()
        {
            string sidechain_folder = @"..\..\..\..\..\assets";
            using (SidechainIdentifier.Create("enigma", sidechain_folder))
            {
                var fedFolder = new TestFederationFolder();

                //
                // Act as federation members.
                //
                //The actor communicates with each Federation Member and they follow the process described in the Generate Federation Member Key Pairs use case.
                //this calls the command line console to generate the keys and send their public key to the SidechainGenerator.
                fedFolder.RunFedKeyPairGen("member1", "pass1");
                fedFolder.RunFedKeyPairGen("member2", "pass2");
                fedFolder.RunFedKeyPairGen("member3", "pass3");

                //give file operations a chance to complete
                await Task.Delay(2000);

                fedFolder.DistributeKeys(new[] {"member1", "member2", "member3"});

                //give file operations a chance to complete
                await Task.Delay(2000);

                ////
                //// Act as Sidechain Generator
                ////
                var memberFolderManager = fedFolder.CreateMemberFolderManager();
                var federation = memberFolderManager.LoadFederation(2, 3);
                federation.Members.Count.Should().Be(3);

                memberFolderManager.OutputScriptPubKeyAndAddress(federation, Network.StratisRegTest);
                memberFolderManager.OutputScriptPubKeyAndAddress(federation, SidechainNetwork.SidechainRegTest);

                fedFolder.DistributeScriptAndAddress(new[] { "member1", "member2", "member3"});
            }
        }
    }
}
