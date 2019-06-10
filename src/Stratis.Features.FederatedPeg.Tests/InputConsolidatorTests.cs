using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class InputConsolidatorTests : CrossChainTestBase
    {
        public InputConsolidatorTests()
        {

        }

        [Fact]
        public void ConsolidationTransactionOnlyBuiltWhenWalletHasManyInputs()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);

            var broadcasterManager = new Mock<IBroadcasterManager>();

            var inputConsolidator = new InputConsolidator(
                this.federatedPegBroadcaster,
                this.FederationWalletTransactionHandler,
                this.federationWalletManager,
                broadcasterManager.Object,
                this.federatedPegSettings,
                this.network);

            Assert.ThrowsAny<Exception>(() => inputConsolidator.BuildConsolidatingTransaction());
        }
    }
}
