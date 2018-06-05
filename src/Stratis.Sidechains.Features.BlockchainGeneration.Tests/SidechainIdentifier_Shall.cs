using System;
using System.IO;
using Xunit;
using FluentAssertions;
using Moq;
using NBitcoin;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests
{
    //SidechainIdentifier is a singleton and it not designed to run
    //multiple within a process. Grouping these tests into a collection
    //prevents xUnit from running these tests in parallel.
    [Collection("SidechainIdentifierTests")]
    public class SidechainIdentifier_Shall
    {
        private TestAssets testAssets = new TestAssets();

        private const string SidechainFolder = "SidechainIdentifier_Shall";
        private readonly DirectoryInfo SidechainFolderInfo;

        public SidechainIdentifier_Shall()
        {
            //prepare a sidechain folder
            this.SidechainFolderInfo = Directory.CreateDirectory(SidechainFolder);
            string sidechainJsonFullPath = Path.Combine(SidechainFolderInfo.FullName, "sidechains.json");

            if (!File.Exists(sidechainJsonFullPath))
                File.Copy(@"..\..\..\..\..\assets\sidechains.json", sidechainJsonFullPath);
        }

        ~SidechainIdentifier_Shall()
        {
            if (Directory.Exists(SidechainFolderInfo.FullName))
                Directory.Delete(SidechainFolderInfo.FullName, true);
        }

        [Fact]
        public void be_immutable_in_scope()
        {
            //a SidechainIdenfifier cannot be created with new.
            //this code will generate a compiler error
            //var sidechainIdenifier = new SidechainIdentifier();

            var sidechainInfoProvider = new Mock<ISidechainInfoProvider>();
            sidechainInfoProvider.Setup(m => m.GetSidechainInfo("enigma"))
                .Returns(this.testAssets.GetSidechainInfo("enigma", 0));

            sidechainInfoProvider.Setup(m => m.VerifyFolder(It.IsAny<string>()));

            using (var sidechainIdentifier = SidechainIdentifier.Create("enigma", sidechainInfoProvider.Object))
            {
                sidechainIdentifier.Name.Should().Be("enigma");

                //the name cannot be changed
                //this code will generate a compiler error
                //sidechainIdenifier.Name = "another name";

                //you can't call create twice in a scope
                Action act = () => SidechainIdentifier.Create("impossible", sidechainInfoProvider.Object);
                act.Should().Throw<InvalidOperationException>()
                    .WithMessage("SidechainIdentifier is immutable.");
            }

            var sidechainIdentifier2 = SidechainIdentifier.Create("possible", sidechainInfoProvider.Object);
            sidechainIdentifier2.Name.Should().Be("possible");
            sidechainIdentifier2.Dispose();
        }

        [Fact]
        public void createable_from_args()
        {
            var args = new string[]{"-sidechainName=enigma", $"-datadir={SidechainFolderInfo.FullName}", "-another=no_used"};
            var sidechainIdentifier = SidechainIdentifier.CreateFromArgs(args);
            sidechainIdentifier.Name.Should().Be("enigma");
            sidechainIdentifier.Dispose();
        }

        [Fact]
        public void createable_from_args_requires_sidechain_name()
        {
            var args = new string[] { $"-datadir={SidechainFolderInfo.FullName}", "-another=no_used" };
            Action act = () => SidechainIdentifier.CreateFromArgs(args);
            act.Should().Throw<ArgumentException>()
                .WithMessage("A -sidechainName arg must be specified.");
        }
    }
}