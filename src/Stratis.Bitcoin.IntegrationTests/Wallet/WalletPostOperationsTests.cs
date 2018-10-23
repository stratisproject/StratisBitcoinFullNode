using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public class WalletPostOperationsTests : IDisposable
    {
        private readonly NodeBuilder builder;

        private readonly Network network;

        private CoreNode Node { get; }

        private string WalletName => "wallet-with-funds";

        private string WalletFilePath { get; set; }

        public WalletPostOperationsTests()
        {
            this.network = new StratisRegTest();
            this.builder = NodeBuilder.Create("WalletPostOperationsTests");
            this.Node = this.builder.CreateStratisPosNode(this.network).NotInIBD();
            this.builder.StartAll();
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }

        private void AddAndLoadWalletFileToWalletFolder()
        {
            string walletsFolderPath = this.Node.FullNode.DataFolder.WalletPath;
            string filename = "wallet-with-funds.wallet.json";
            this.WalletFilePath = Path.Combine(walletsFolderPath, filename);
            File.Copy(Path.Combine("Wallet", "Data", filename), this.WalletFilePath, true);

            var result = $"http://localhost:{this.Node.ApiPort}/api".AppendPathSegment("wallet/load").PostJsonAsync(new WalletLoadRequest
            {
                Name = this.WalletName,
                Password = "123456"
            }).Result;
        }

        [Fact]
        public async Task CreateAnAccountWhenAnUnusedAccountExists()
        {
            // Arrange.
            this.AddAndLoadWalletFileToWalletFolder();

            // Make sure the wallet has two account.
            string newAccountName = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/account")
                .PostJsonAsync(new { walletName = this.WalletName, password = "123456" })
                .ReceiveJson<string>();

            IEnumerable<string> accountsNames = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName = this.WalletName })
                .GetJsonAsync<IEnumerable<string>>();

            accountsNames.Count().Should().Be(2);

            // Make sure the first account is used, i.e, it has transactions.
            WalletHistoryModel firstAccountHistory = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<WalletHistoryModel>();

            firstAccountHistory.AccountsHistoryModel.Should().NotBeEmpty();
            firstAccountHistory.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

            // Make sure the second account is not used, i.e, it doesn't have transactions.
            WalletHistoryModel secondAccountHistory = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 1" })
                .GetJsonAsync<WalletHistoryModel>();

            secondAccountHistory.AccountsHistoryModel.Should().NotBeEmpty();
            secondAccountHistory.AccountsHistoryModel.First().TransactionsHistory.Should().BeEmpty();

            // Act.
            string newAddedAccountName = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/account")
                .PostJsonAsync(new { walletName = this.WalletName, password = "123456" })
                .ReceiveJson<string>();

            // Assert.
            // Check the returned account name is the second one.
            newAddedAccountName.Should().Be(newAccountName);

            // Check that the number of accounts found in the wallet hasn't changed.
            IEnumerable<string> newAccountsNames = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName = this.WalletName })
                .GetJsonAsync<IEnumerable<string>>();

            accountsNames.Count().Should().Be(2);
        }

        [Fact]
        public async Task CreateAnAccountWhenNoUnusedAccountExists()
        {
            // Arrange.
            this.AddAndLoadWalletFileToWalletFolder();

            // Make sure the wallet only has one account.
            IEnumerable<string> accountsNames = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName = this.WalletName })
                .GetJsonAsync<IEnumerable<string>>();

            accountsNames.Should().ContainSingle();
            accountsNames.Single().Should().Be("account 0");

            // Make sure the account is used, i.e, it has transactions.
            WalletHistoryModel history = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<WalletHistoryModel>();

            history.AccountsHistoryModel.Should().NotBeEmpty();
            history.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

            // Act.
            string newAccountName = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/account")
                .PostJsonAsync(new { walletName = this.WalletName, password = "123456" })
                .ReceiveJson<string>();

            // Assert.
            newAccountName.Should().Be("account 1");
        }

        [Fact]
        public async Task GetWalletFilesWhenNoFilesArePresent()
        {
            // Act.
            WalletFileModel walletFileModel = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/files")
                .GetJsonAsync<WalletFileModel>();

            // Assert.
            walletFileModel.WalletsPath.Should().Be(this.Node.FullNode.DataFolder.WalletPath);
            walletFileModel.WalletsFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task GetUnusedAddressesInAccountWhenAddressesNeedToBeCreated()
        {
            // Arrange.
            this.AddAndLoadWalletFileToWalletFolder();

            AddressesModel addressesModel = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<AddressesModel>();

            int unusedReceiveAddressesCount = addressesModel.Addresses.Count(a => !a.IsUsed && !a.IsChange);

            // Act.
            IEnumerable<string> unusedaddresses = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/unusedAddresses")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0", count = unusedReceiveAddressesCount + 5 })
                .GetJsonAsync<IEnumerable<string>>();

            // Assert.
            unusedaddresses.Count().Should().Be(unusedReceiveAddressesCount + 5);
        }

        [Fact]
        public async Task GetUnusedAddressesInAccountWhenNoAddressesNeedToBeCreated()
        {
            // Arrange.
            this.AddAndLoadWalletFileToWalletFolder();

            AddressesModel addressesModel = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<AddressesModel>();

            int totalAddressesCount = addressesModel.Addresses.Count();

            int unusedReceiveAddressesCount = addressesModel.Addresses.Count(a => !a.IsUsed && !a.IsChange);

            // Act.
            IEnumerable<string> unusedaddresses = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/unusedAddresses")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0", count = unusedReceiveAddressesCount - 1 })
                .GetJsonAsync<IEnumerable<string>>();

            // Assert.
            AddressesModel addressesModelAgain = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<AddressesModel>();

            addressesModelAgain.Addresses.Count().Should().Be(totalAddressesCount);

        }

        [Fact]
        public async Task RemoveAllTransactionsFromWallet()
        {
            // Arrange.
            this.AddAndLoadWalletFileToWalletFolder();

            // Make sure the account is used, i.e, it has transactions.
            WalletHistoryModel history = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<WalletHistoryModel>();

            history.AccountsHistoryModel.Should().NotBeEmpty();
            history.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

            // Act.
            HashSet<(uint256 transactionId, DateTimeOffset creationTime)> results = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/remove-transactions")
                .SetQueryParams(new { walletName = this.WalletName, all = true })
                .DeleteAsync()
                .ReceiveJson<HashSet<(uint256 transactionId, DateTimeOffset creationTime)>>();

            // Assert.
            WalletHistoryModel historyAgain = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<WalletHistoryModel>();

            historyAgain.AccountsHistoryModel.Should().NotBeEmpty();
            historyAgain.AccountsHistoryModel.First().TransactionsHistory.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveTransactionsWithoutIdsOrAllFlagFromWallet()
        {
            // Arrange.
            this.AddAndLoadWalletFileToWalletFolder();

            // Make sure the account is used, i.e, it has transactions.
            WalletHistoryModel history = await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/history")
                .SetQueryParams(new { walletName = this.WalletName, accountName = "account 0" })
                .GetJsonAsync<WalletHistoryModel>();

            history.AccountsHistoryModel.Should().NotBeEmpty();
            history.AccountsHistoryModel.First().TransactionsHistory.Should().NotBeEmpty();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.Node.ApiPort}/api"
                .AppendPathSegment("wallet/remove-transactions")
                .SetQueryParams(new { walletName = this.WalletName })
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
}
