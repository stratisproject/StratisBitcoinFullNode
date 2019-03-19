using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Controllers;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.ControllersTests
{
    public class FederationWalletControllerTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IFederationWalletManager walletManager;
        private readonly IFederationWalletSyncManager walletSyncManager;
        private readonly IConnectionManager connectionManager;
        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IWithdrawalHistoryProvider withdrawalHistoryProvider;

        private readonly FederationWalletController controller;
        private readonly FederationWallet fedWallet;

        public FederationWalletControllerTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.walletManager = Substitute.For<IFederationWalletManager>();
            this.walletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.connectionManager = Substitute.For<IConnectionManager>();
            this.network = new StratisTest();

            this.chain = new ConcurrentChain(this.network);

            ChainedHeader tip = ChainedHeadersHelper.CreateConsecutiveHeaders(100, ChainedHeadersHelper.CreateGenesisChainedHeader(this.network), true, null, this.network).Last();
            this.chain.SetTip(tip);


            this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
            this.withdrawalHistoryProvider = Substitute.For<IWithdrawalHistoryProvider>();

            this.controller = new FederationWalletController(this.loggerFactory, this.walletManager, this.walletSyncManager,
                this.connectionManager, this.network, this.chain, this.dateTimeProvider, this.withdrawalHistoryProvider);

            this.fedWallet = new FederationWallet();
            this.fedWallet.Network = this.network;
            this.fedWallet.LastBlockSyncedHeight = 999;
            this.fedWallet.CreationTime = DateTimeOffset.Now;

            this.walletManager.GetWallet().Returns(this.fedWallet);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void GetGeneralInfo()
        {
            this.connectionManager.ConnectedPeers.Returns(info => new NetworkPeerCollection());

            IActionResult result = this.controller.GetGeneralInfo();
            WalletGeneralInfoModel model = ActionResultToModel<WalletGeneralInfoModel>(result);

            Assert.Equal(this.fedWallet.CreationTime, model.CreationTime);
            Assert.Equal(this.fedWallet.LastBlockSyncedHeight, model.LastBlockSyncedHeight);
            Assert.Equal(this.fedWallet.Network, model.Network);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void GetBalance()
        {
            this.fedWallet.MultiSigAddress = new MultiSigAddress();

            IActionResult result = this.controller.GetBalance();
            WalletBalanceModel model = ActionResultToModel<WalletBalanceModel>(result);

            Assert.Single(model.AccountsBalances);
            Assert.Equal(CoinType.Stratis, model.AccountsBalances.First().CoinType);
            Assert.Equal(0, model.AccountsBalances.First().AmountConfirmed.Satoshi);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void GetHistory()
        {
            var withdrawals = new List<WithdrawalModel>() {new WithdrawalModel(), new WithdrawalModel()};

            this.withdrawalHistoryProvider.GetHistory(0).ReturnsForAnyArgs(withdrawals);

            IActionResult result = this.controller.GetHistory(5);
            List<WithdrawalModel> model = ActionResultToModel<List<WithdrawalModel>>(result);

            Assert.Equal(withdrawals.Count, model.Count);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void Sync()
        {
            ChainedHeader header = this.chain.Tip;

            bool called = false;
            this.walletSyncManager.When(x => x.SyncFromHeight(header.Height)).Do(info => called = true);

            this.controller.Sync(new HashModel() { Hash = header.HashBlock.ToString() });

            Assert.True(called);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void EnableFederation()
        {
            bool called = false;
            this.walletManager.When(x => x.EnableFederation(null)).Do(info => called = true);

            this.controller.EnableFederation(new EnableFederationRequest());

            Assert.True(called);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void RemoveTransactions()
        {
            var hashSet = new HashSet<(uint256, DateTimeOffset)>();
            hashSet.Add((uint256.One, DateTimeOffset.MinValue));

            this.walletManager.RemoveAllTransactions().Returns(info => hashSet);

            IActionResult result = this.controller.RemoveTransactions(new RemoveFederationTransactionsModel());

            IEnumerable<RemovedTransactionModel> model = ActionResultToModel<IEnumerable<RemovedTransactionModel>>(result);

            Assert.Single(model);
        }

        private T ActionResultToModel<T>(IActionResult result) where T : class
        {
            T model = (result as JsonResult).Value as T;
            return model;
        }
    }
}
