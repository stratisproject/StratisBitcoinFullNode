using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class WalletRPCControllerTests : LogsTestBase
    {
        private WalletRPCController controller;
        private ChainIndexer chain;
        private Network network;
        private NodeSettings nodeSettings;
        private WalletSettings walletSettings;
        private readonly StoreSettings storeSettings;
        private readonly List<ActionDescriptor> descriptors;

        private readonly Mock<IBlockStore> blockStore;
        private readonly Mock<IBroadcasterManager> broadCastManager;
        private readonly Mock<IConsensusManager> consensusManager;
        private readonly Mock<IFullNode> fullNode;
        private readonly Mock<IScriptAddressReader> scriptAddressReader;
        private readonly Mock<IWalletManager> walletManager;
        private readonly Mock<IWalletTransactionHandler> walletTransactionHandler;

        public WalletRPCControllerTests()
        {
            this.network = KnownNetworks.TestNet;
            this.blockStore = new Mock<IBlockStore>();
            this.broadCastManager = new Mock<IBroadcasterManager>();
            this.consensusManager = new Mock<IConsensusManager>();
            this.fullNode = new Mock<IFullNode>();
            this.scriptAddressReader = new Mock<IScriptAddressReader>();
            this.nodeSettings = new NodeSettings(this.Network);
            this.storeSettings = new StoreSettings(this.nodeSettings);
            this.walletManager = new Mock<IWalletManager>();
            this.walletSettings = new WalletSettings(this.nodeSettings);
            this.walletTransactionHandler = new Mock<IWalletTransactionHandler>();

            this.controller = 
                new WalletRPCController(
                        this.blockStore.Object, 
                        this.broadCastManager.Object, 
                        this.chain, 
                        this.consensusManager.Object, 
                        this.fullNode.Object, 
                        this.LoggerFactory.Object,
                        this.network, 
                        this.scriptAddressReader.Object,
                        this.storeSettings,
                        this.walletManager.Object,
                        this.walletSettings,
                        this.walletTransactionHandler.Object
                        );
        }

        [Fact]
        public void GetNewAddress_WithAccountParameterSet_ThrowsException()
        {
            RPCServerException exception = Assert.Throws<RPCServerException>(() =>
            {
                NewAddressModel result = this.controller.GetNewAddress("test");
            });

            Assert.NotNull(exception);
            Assert.Equal("Use of 'account' parameter has been deprecated", exception.Message);
        }

        [Fact]
        public void GetNewAddress_WithIncompatibleAddressType_ThrowsException()
        {
            RPCServerException exception = Assert.Throws<RPCServerException>(() =>
            {
                NewAddressModel result = this.controller.GetNewAddress("", "x");
            });

            Assert.NotNull(exception);
            Assert.Equal("Only address type 'legacy' is currently supported.", exception.Message);
        }

        [Fact]
        public void GetUnusedAddress_WithAccountParameterSet_ThrowsException()
        {
            RPCServerException exception = Assert.Throws<RPCServerException>(() =>
            {
                NewAddressModel result = this.controller.GetUnusedAddress("test", "");
            });

            Assert.NotNull(exception);
            Assert.Equal("Use of 'account' parameter has been deprecated", exception.Message);
        }

        [Fact]
        public void GetUnusedAddress_WithIncompatibleAddressType_ThrowsException()
        {
            RPCServerException exception = Assert.Throws<RPCServerException>(() =>
            {
                NewAddressModel result = this.controller.GetUnusedAddress("", "x");
            });

            Assert.NotNull(exception);
            Assert.Equal("Only address type 'legacy' is currently supported.", exception.Message);
        }
    }
}
