using FluentAssertions;
using System;
using System.Linq;
using Xunit;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Api;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;
using System.Diagnostics;

namespace Stratis.Sidechains.Commands.Tests
{
    public class NewSidechainCommandTests : ScriptBasedTest
    {
        [Fact]
        public void NewSidechain_should_actually_add_sidechain()
        {
            NewSideChainCommands_should_actually_add_sidechain("newSidechain.ps1");            
        }

        [Fact]
        public void NewSidechainUsingApi_should_actually_add_sidechain()
        {
            Action<IFullNodeBuilder> buildCallBack = (n) => n.UseSidechains().UseApi().MockIBD();
            using (var nodeBuilder = NodeBuilder.Create(new StackTrace().GetFrame(0).GetMethod().Name))
            {
                var node = nodeBuilder.CreatePosSidechainNode("enigma", true, buildCallBack);

                NewSideChainCommands_should_actually_add_sidechain("newSidechainUsingApi.ps1", node.DataFolder, node.ApiPort);
            }
        }

        private void NewSideChainCommands_should_actually_add_sidechain(string scriptName, string folder = null, int? ApiPort = null)
        {
            var newSidechainName = "anotherEnigma";
            var getSidechainScript = ApiPort.HasValue ? "getSidechainsUsingApi.ps1" : "getSidechains.ps1";
            var beforeAddingChain = RunWorkingScript(getSidechainScript, folder, true, ApiPort);
            beforeAddingChain.Select(r => (SidechainInfo)r.BaseObject).Should()
                .NotContain(s => s.ChainName == newSidechainName, "we want to make sure the chain didn't exist before the test");

            var chainsCount = beforeAddingChain.Count;
            
            var results = RunWorkingScript(scriptName, folder, false, ApiPort);

            var afterAddingChain = RunWorkingScript(getSidechainScript, folder, false, ApiPort);
            var newChainsCount = afterAddingChain.Count;

            newChainsCount.Should().Be(chainsCount + 1);
            afterAddingChain[chainsCount].BaseObject.Should().NotBeNull();
            ((SidechainInfo)afterAddingChain[chainsCount].BaseObject).ChainName.Should().Be(newSidechainName);
        }

        private void NewSidechainCommands_should_error_if_chain_already_exists(string scriptName, string folder = null, int? apiPort = null)
        {
            var newSidechainName = TestAssets.EnigmaChainName;
            var beforeAddingChain = RunWorkingScript("getSidechains.ps1", folder, true, apiPort);
            beforeAddingChain.Select(r => (SidechainInfo)r.BaseObject).Should()
                .Contain(s => s.ChainName == newSidechainName, "we want to make sure the chain didn't exist before the test");

            var errors = RunFailingScript(scriptName, folder, false, apiPort);
            errors.Count.Should().Be(1);
            errors[0].FullyQualifiedErrorId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void NewSidechain_should_error_if_chain_already_exists()
        {
            NewSidechainCommands_should_error_if_chain_already_exists("newSidechain_already_exists.ps1");
        }

        [Fact]
        public void NewSidechainUsingApi_should_error_if_chain_already_exists()
        {
            Action<IFullNodeBuilder> buildCallBack = (n) => n.UseSidechains().UseApi().MockIBD();
            using (var nodeBuilder = NodeBuilder.Create(new StackTrace().GetFrame(0).GetMethod().Name))
            {
                var node = nodeBuilder.CreatePosSidechainNode("enigma", true, buildCallBack);
                NewSidechainCommands_should_error_if_chain_already_exists("newSidechainUsingApi_already_exists.ps1", node.DataFolder, node.ApiPort);
            }

        }
    }
}
