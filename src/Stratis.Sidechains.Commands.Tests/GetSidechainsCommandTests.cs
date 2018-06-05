using System.Management.Automation;
using Xunit;
using FluentAssertions;
using System.Linq;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;

namespace Stratis.Sidechains.Commands.Tests
{
    public class GetSidechainsCommandTests : ScriptBasedTest
    {
        [Fact]
        public void GetSidechains_when_no_sidechain_name_is_passed_should_return_all_sidechains()
        {
            var results = RunWorkingScript("getsideChains.ps1");
            results.Count.Should().Be(2);
            results.Select(r => r.BaseObject).Should().AllBeOfType<SidechainInfo>();
            ((SidechainInfo)results[0].BaseObject).ChainName.Should().Be(TestAssets.EnigmaChainName);
            ((SidechainInfo)results[1].BaseObject).ChainName.Should().Be(TestAssets.MysteryChainName);
        }

        [Fact]
        public void GetSidechains_when_sidechain_doesnt_exist_should_throw()
        {
            var error = RunFailingScript("getsideChains_doesnt_exist.ps1");
            error.Count.Should().Be(1);
        }

        [Fact]
        public void GetSidechains_when_sidechain_exist_should_only_return_that_chain()
        {
            var results = RunWorkingScript("getsideChains_does_exist.ps1");
            results.Count.Should().Be(1);
            ((SidechainInfo)results[0].BaseObject).ChainName.Should().Be(TestAssets.MysteryChainName);

        }
    }
}
