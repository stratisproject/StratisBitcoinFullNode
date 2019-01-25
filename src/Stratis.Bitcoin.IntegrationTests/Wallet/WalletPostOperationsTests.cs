using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DBreeze.Utils;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    /// <summary>
    /// This class contains tests that must be run with a fresh node.
    /// </summary>
    public class WalletPostOperationsTests
    {
        private readonly Network network;

        internal readonly string walletWithFundsName = "wallet-with-funds";

        private string walletFilePath;

        public WalletPostOperationsTests()
        {
            this.network = new StratisRegTest();
        }

        private void AddAndLoadWalletFileToWalletFolder(CoreNode node)
        {
            string walletsFolderPath = node.FullNode.DataFolder.WalletPath;
            string filename = "wallet-with-funds.wallet.json";
            this.walletFilePath = Path.Combine(walletsFolderPath, filename);
            File.Copy(Path.Combine("Wallet", "Data", filename), this.walletFilePath, true);

            var result = $"http://localhost:{node.ApiPort}/api".AppendPathSegment("wallet/load").PostJsonAsync(new WalletLoadRequest
            {
                Name = this.walletWithFundsName,
                Password = "123456"
            }).Result;
        }

        [Fact]
        public async Task CreateAnAccountWhenAnUnusedAccountExists()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                this.AddAndLoadWalletFileToWalletFolder(node);

                // Make sure the wallet has two account.
                string newAccountName = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/account")
                    .PostJsonAsync(new { walletName = this.walletWithFundsName, password = "123456" })
                    .ReceiveJson<string>();

                IEnumerable<string> accountsNames = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/accounts")
                    .SetQueryParams(new { walletName = this.walletWithFundsName })
                    .GetJsonAsync<IEnumerable<string>>();

                accountsNames.Count().Should().Be(2);

                // Make sure the first account is used, i.e, it has transactions.
                WalletHistoryModel firstAccountHistory = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/history")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                    .GetJsonAsync<WalletHistoryModel>();

                firstAccountHistory.AccountsHistoryModel.Should().NotBeEmpty();
                firstAccountHistory.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

                // Make sure the second account is not used, i.e, it doesn't have transactions.
                WalletHistoryModel secondAccountHistory = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/history")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 1" })
                    .GetJsonAsync<WalletHistoryModel>();

                secondAccountHistory.AccountsHistoryModel.Should().NotBeEmpty();
                secondAccountHistory.AccountsHistoryModel.First().TransactionsHistory.Should().BeEmpty();

                // Act.
                string newAddedAccountName = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/account")
                    .PostJsonAsync(new { walletName = this.walletWithFundsName, password = "123456" })
                    .ReceiveJson<string>();

                // Assert.
                // Check the returned account name is the second one.
                newAddedAccountName.Should().Be(newAccountName);

                // Check that the number of accounts found in the wallet hasn't changed.
                IEnumerable<string> newAccountsNames = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/accounts")
                    .SetQueryParams(new { walletName = this.walletWithFundsName })
                    .GetJsonAsync<IEnumerable<string>>();

                accountsNames.Count().Should().Be(2);
            }
        }

        [Fact]
        public async Task CreateAnAccountWhenNoUnusedAccountExists()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                this.AddAndLoadWalletFileToWalletFolder(node);

                // Make sure the wallet only has one account.
                IEnumerable<string> accountsNames = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName = this.walletWithFundsName })
                .GetJsonAsync<IEnumerable<string>>();

                accountsNames.Should().ContainSingle();
                accountsNames.Single().Should().Be("account 0");

                // Make sure the account is used, i.e, it has transactions.
                WalletHistoryModel history = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/history")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                    .GetJsonAsync<WalletHistoryModel>();

                history.AccountsHistoryModel.Should().NotBeEmpty();
                history.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

                // Act.
                string newAccountName = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/account")
                    .PostJsonAsync(new { walletName = this.walletWithFundsName, password = "123456" })
                    .ReceiveJson<string>();

                // Assert.
                newAccountName.Should().Be("account 1");
            }
        }

        [Fact]
        public async Task GetWalletFilesWhenNoFilesArePresent()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                // Act.
                WalletFileModel walletFileModel = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/files")
                .GetJsonAsync<WalletFileModel>();

                // Assert.
                walletFileModel.WalletsPath.Should().Be(node.FullNode.DataFolder.WalletPath);
                walletFileModel.WalletsFiles.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetUnusedAddressesInAccountWhenAddressesNeedToBeCreated()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                this.AddAndLoadWalletFileToWalletFolder(node);

                AddressesModel addressesModel = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/addresses")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                    .GetJsonAsync<AddressesModel>();

                int unusedReceiveAddressesCount = addressesModel.Addresses.Count(a => !a.IsUsed && !a.IsChange);

                // Act.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0", count = unusedReceiveAddressesCount + 5 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Assert.
                unusedaddresses.Count().Should().Be(unusedReceiveAddressesCount + 5);
            }
        }

        [Fact]
        public async Task GetUnusedAddressesInAccountWhenNoAddressesNeedToBeCreated()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                this.AddAndLoadWalletFileToWalletFolder(node);

                AddressesModel addressesModel = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                .GetJsonAsync<AddressesModel>();

                int totalAddressesCount = addressesModel.Addresses.Count();

                int unusedReceiveAddressesCount = addressesModel.Addresses.Count(a => !a.IsUsed && !a.IsChange);

                // Act.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0", count = unusedReceiveAddressesCount - 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Assert.
                AddressesModel addressesModelAgain = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/addresses")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                    .GetJsonAsync<AddressesModel>();

                addressesModelAgain.Addresses.Count().Should().Be(totalAddressesCount);
            }
        }

        [Fact]
        public async Task RemoveAllTransactionsFromWallet()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                this.AddAndLoadWalletFileToWalletFolder(node);

                // Make sure the account is used, i.e, it has transactions.
                WalletHistoryModel history = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                .GetJsonAsync<WalletHistoryModel>();

                history.AccountsHistoryModel.Should().NotBeEmpty();
                history.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

                // Act.
                HashSet<(uint256 transactionId, DateTimeOffset creationTime)> results = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/remove-transactions")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, all = true })
                    .DeleteAsync()
                    .ReceiveJson<HashSet<(uint256 transactionId, DateTimeOffset creationTime)>>();

                // Assert.
                WalletHistoryModel historyAgain = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/history")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                    .GetJsonAsync<WalletHistoryModel>();

                historyAgain.AccountsHistoryModel.Should().NotBeEmpty();
                historyAgain.AccountsHistoryModel.First().TransactionsHistory.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task RemoveTransactionsWithoutIdsOrAllFlagFromWallet()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                this.AddAndLoadWalletFileToWalletFolder(node);
                
                // Make sure the account is used, i.e, it has transactions.
                WalletHistoryModel history = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                .GetJsonAsync<WalletHistoryModel>();

                history.AccountsHistoryModel.Should().NotBeEmpty();
                history.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

                // Act.
                Func<Task> act = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/remove-transactions")
                    .SetQueryParams(new { walletName = this.walletWithFundsName })
                    .DeleteAsync()
                    .ReceiveJson<HashSet<(uint256 transactionId, DateTimeOffset creationTime)>>();

                // Assert.
                var exception = act.Should().Throw<FlurlHttpException>().Which;
                var response = exception.Call.Response;

                ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                List<ErrorModel> errors = errorResponse.Errors;

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be("Transaction ids need to be specified if the 'all' flag is not set.");
            }
        }


        [Fact]
        public async Task GetSpendableTransactionsInAccountAllowUnconfirmed()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();
                CoreNode miningNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();

                this.AddAndLoadWalletFileToWalletFolder(node);
                TestHelper.ConnectAndSync(node, miningNode);

                // Act.
                var transactionsAllowUnconfirmed = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/spendable-transactions")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0" })
                    .GetJsonAsync<SpendableTransactionsModel>();

                var transactionsOnlyConfirmed = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("wallet/spendable-transactions")
                    .SetQueryParams(new { walletName = this.walletWithFundsName, accountName = "account 0", minConfirmations = 1 })
                    .GetJsonAsync<SpendableTransactionsModel>();

                // Assert.
                transactionsAllowUnconfirmed.SpendableTransactions.Should().HaveCount(30);
                transactionsAllowUnconfirmed.SpendableTransactions.Sum(st => st.Amount).Should().Be(new Money(142290299995400));

                transactionsOnlyConfirmed.SpendableTransactions.Should().HaveCount(29);
                transactionsOnlyConfirmed.SpendableTransactions.Sum(st => st.Amount).Should().Be(new Money(142190299995400));
            }
        }

        [Fact]
        public async Task SendingFromOneAddressToFiftyAddresses()
        {
            int sendingAccountBalanceOnStart = 98000596;
            int receivingAccountBalanceOnStart = 0;

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                // Create a sending and a receiving node.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();
                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Check balances.
                WalletBalanceModel sendingNodeBalances = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                AccountBalanceModel sendingAccountBalance = sendingNodeBalances.AccountsBalances.Single();
                (sendingAccountBalance.AmountConfirmed + sendingAccountBalance.AmountUnconfirmed).Should().Be(new Money(sendingAccountBalanceOnStart, MoneyUnit.BTC));

                WalletBalanceModel receivingNodeBalances = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                AccountBalanceModel receivingAccountBalance = receivingNodeBalances.AccountsBalances.Single();
                (receivingAccountBalance.AmountConfirmed + receivingAccountBalance.AmountUnconfirmed).Should().Be(new Money(receivingAccountBalanceOnStart));

                // Act.
                // Get 50 addresses to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 50 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with 50 recipients.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeType = "low",
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = "1"
                        }).ToList()
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                // Assert.
                // The sending node should have 50 (+ fee) fewer coins.
                sendingNodeBalances = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                sendingAccountBalance = sendingNodeBalances.AccountsBalances.Single();
                (sendingAccountBalance.AmountConfirmed + sendingAccountBalance.AmountUnconfirmed).Should().Be(new Money(sendingAccountBalanceOnStart - 50 - buildTransactionModel.Fee.ToDecimal(MoneyUnit.BTC), MoneyUnit.BTC));

                // Mine and sync so that we make sure the receiving node is up to date.
                TestHelper.MineBlocks(sendingNode, 1);

                // The receiving node should have 50 more coins.
                receivingNodeBalances = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                receivingAccountBalance = receivingNodeBalances.AccountsBalances.Single();
                (receivingAccountBalance.AmountConfirmed + receivingAccountBalance.AmountUnconfirmed).Should().Be(new Money(receivingAccountBalanceOnStart + 50, MoneyUnit.BTC));
            }
        }

        [Fact]
        public async Task SendingFromManyAddressesToOneAddress()
        {
            int sendingAccountBalanceOnStart = 98000596;
            int receivingAccountBalanceOnStart = 0;

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                // Create a sending and a receiving node.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithWallet().Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithWallet().Start();

                // Mine a few blocks to fund the sending node and connect the nodes.
                IEnumerable<string> addressesToFund = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 150 })
                    .GetJsonAsync<IEnumerable<string>>();

                foreach (string address in addressesToFund)
                {
                    TestHelper.MineBlocks(sendingNode, 1, syncNode: false, miningAddress: address);
                }
                
                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Check balances.
                WalletBalanceModel sendingNodeBalances = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                AccountBalanceModel sendingAccountBalance = sendingNodeBalances.AccountsBalances.Single();
                (sendingAccountBalance.AmountConfirmed + sendingAccountBalance.AmountUnconfirmed).Should().Be(new Money(sendingAccountBalanceOnStart, MoneyUnit.BTC));

                WalletBalanceModel receivingNodeBalances = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                AccountBalanceModel receivingAccountBalance = receivingNodeBalances.AccountsBalances.Single();
                (receivingAccountBalance.AmountConfirmed + receivingAccountBalance.AmountUnconfirmed).Should().Be(new Money(receivingAccountBalanceOnStart));

                // Check max spendable amount.
                var maxBalanceResponse = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/maxbalance")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", feetype = "low", allowunconfirmed = true })
                    .GetJsonAsync<MaxSpendableAmountModel>();

                Money totalToSpend = maxBalanceResponse.MaxSpendableAmount + maxBalanceResponse.Fee;

                // Act.
                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with 50 recipients.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeAmount = maxBalanceResponse.Fee.ToString(),
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = maxBalanceResponse.MaxSpendableAmount.ToString()
                        }).ToList()
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                    await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                // Assert.
                // The sending node should have 50 (+ fee) fewer coins.
                sendingNodeBalances = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                sendingAccountBalance = sendingNodeBalances.AccountsBalances.Single();
                (sendingAccountBalance.AmountConfirmed + sendingAccountBalance.AmountUnconfirmed).Should().Be(new Money(sendingAccountBalanceOnStart, MoneyUnit.BTC) - totalToSpend);

                // Mine and sync so that we make sure the receiving node is up to date.
                TestHelper.MineBlocks(sendingNode, 1);

                // The receiving node should have 50 more coins.
                receivingNodeBalances = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                receivingAccountBalance = receivingNodeBalances.AccountsBalances.Single();
                (receivingAccountBalance.AmountConfirmed + receivingAccountBalance.AmountUnconfirmed).Should().Be(new Money(receivingAccountBalanceOnStart) + maxBalanceResponse.MaxSpendableAmount);
            }
        }

        [Fact]
        public async Task SendingATransactionWithAnOpReturn()
        {
            int sendingAccountBalanceOnStart = 98000596;
            int receivingAccountBalanceOnStart = 0;

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                // Create a sending and a receiving node.
                CoreNode sendingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Miner).Start();
                CoreNode receivingNode = builder.CreateStratisPosNode(this.network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest150Listener).Start();
                TestHelper.ConnectAndSync(sendingNode, receivingNode);

                // Check balances.
                WalletBalanceModel sendingNodeBalances = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                AccountBalanceModel sendingAccountBalance = sendingNodeBalances.AccountsBalances.Single();
                (sendingAccountBalance.AmountConfirmed + sendingAccountBalance.AmountUnconfirmed).Should().Be(new Money(sendingAccountBalanceOnStart, MoneyUnit.BTC));

                WalletBalanceModel receivingNodeBalances = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                AccountBalanceModel receivingAccountBalance = receivingNodeBalances.AccountsBalances.Single();
                (receivingAccountBalance.AmountConfirmed + receivingAccountBalance.AmountUnconfirmed).Should().Be(new Money(receivingAccountBalanceOnStart));

                // Act.
                // Get an address to send to.
                IEnumerable<string> unusedaddresses = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/unusedAddresses")
                    .SetQueryParams(new { walletName = "mywallet", accountName = "account 0", count = 1 })
                    .GetJsonAsync<IEnumerable<string>>();

                // Build and send the transaction with an Op_Return.
                WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/build-transaction")
                    .PostJsonAsync(new BuildTransactionRequest
                    {
                        WalletName = "mywallet",
                        AccountName = "account 0",
                        FeeType = "low",
                        Password = "password",
                        ShuffleOutputs = true,
                        AllowUnconfirmed = true,
                        Recipients = unusedaddresses.Select(address => new RecipientModel
                        {
                            DestinationAddress = address,
                            Amount = "1"
                        }).ToList(),
                        OpReturnData = "some data to send",
                        OpReturnAmount = "1"
                    })
                    .ReceiveJson<WalletBuildTransactionModel>();

                await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/send-transaction")
                    .PostJsonAsync(new SendTransactionRequest
                    {
                        Hex = buildTransactionModel.Hex
                    })
                    .ReceiveJson<WalletSendTransactionModel>();

                // Assert.
                // Mine and sync so that we make sure the receiving node is up to date.
                TestHelper.MineBlocks(sendingNode, 1);
                TestHelper.WaitForNodeToSync(sendingNode, receivingNode);

                // The receiving node should have coins.
                receivingNodeBalances = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                receivingAccountBalance = receivingNodeBalances.AccountsBalances.Single();
                (receivingAccountBalance.AmountConfirmed).Should().Be(new Money(receivingAccountBalanceOnStart + 1, MoneyUnit.BTC));

                // The sending node should have fewer coins.
                sendingNodeBalances = await $"http://localhost:{sendingNode.ApiPort}/api"
                    .AppendPathSegment("wallet/balance")
                    .SetQueryParams(new { walletName = "mywallet" })
                    .GetJsonAsync<WalletBalanceModel>();

                sendingAccountBalance = sendingNodeBalances.AccountsBalances.Single();
                (sendingAccountBalance.AmountConfirmed).Should().Be(new Money(sendingAccountBalanceOnStart + 4 - 2, MoneyUnit.BTC));

                // Check the transaction.
                string lastBlockHash = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("consensus/getbestblockhash")
                    .GetJsonAsync<string>();

                BlockTransactionDetailsModel block = await $"http://localhost:{receivingNode.ApiPort}/api"
                    .AppendPathSegment("blockstore/block")
                    .SetQueryParams(new {hash = lastBlockHash, showTransactionDetails = true, outputJson = true})
                    .GetJsonAsync<BlockTransactionDetailsModel>();

                TransactionVerboseModel trx = block.Transactions.SingleOrDefault(t => t.TxId == buildTransactionModel.TransactionId.ToString());
                trx.Should().NotBeNull();

                Vout opReturnOutputFromBlock = trx.VOut.Single(t => t.ScriptPubKey.Type == "nulldata");
                opReturnOutputFromBlock.Value.Should().Be(1);
                var script = opReturnOutputFromBlock.ScriptPubKey.Asm;
                string[] ops = script.Split(" ");
                ops[0].Should().Be("OP_RETURN");
                Encoders.Hex.DecodeData(ops[1]).Should().BeEquivalentTo("some data to send".ToBytes());
            }
        }
    }
}
