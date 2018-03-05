using System;
using Xunit;
using FluentAssertions;
using Moq;
using NBitcoin;

namespace Stratis.Sidechains.Tests
{
    public class SidechainIdentifier_Shall
    {
        private TestAssets testAssets = new TestAssets();

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
    }
}