using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public class WalletOperationsFixture : IDisposable
    {
        private readonly NodeBuilder builder;

        private readonly Network network;

        public CoreNode Node { get; }

        public string WalletName => "wallet-with-funds";

        public string WalletFilePath { get; }

        public WalletOperationsFixture()
        {
            this.network = new StratisRegTest();
            this.builder = NodeBuilder.Create(this, "WalletOperationsTests");
            CoreNode stratisNode = this.builder.CreateStratisPosNode(this.network).NotInIBD();
            this.builder.StartAll();

            string walletsFolderPath = stratisNode.FullNode.DataFolder.WalletPath;
            string filename = "wallet-with-funds.wallet.json";
            this.WalletFilePath = Path.Combine(walletsFolderPath, filename);
            File.Copy(Path.Combine("Wallet", "Data", filename), this.WalletFilePath, true);

            var result = $"http://localhost:{stratisNode.ApiPort}/api".AppendPathSegment("wallet/load").PostJsonAsync(new WalletLoadRequest
            {
                Name = this.WalletName,
                Password = "123456"
            }).Result;

            this.Node = stratisNode;
        }

        /// <summary>
        /// Create a unique wallet name as wallets with the same name can't be oaded by the same node.
        /// </summary>
        /// <param name="callingMethod">The name of the calling method, most likely the currently running test.</param>
        /// <returns>A unique wallet name.</returns>
        public string GetUniqueWalletName([CallerMemberName] string callingMethod = null)
        {
            return $"wallet-{callingMethod}-{Guid.NewGuid().ToString("N").Substring(0, 3)}";
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }
    }

    public class WalletOperationsTests : IClassFixture<WalletOperationsFixture>
    {
        private readonly CoreNode node;

        private readonly WalletOperationsFixture fixture;

        private readonly string walletName;

        public WalletOperationsTests(WalletOperationsFixture fixture)
        {
            this.node = fixture.Node;
            this.fixture = fixture;
            this.walletName = fixture.WalletName;
        }

        [Fact]
        public async Task CheckBalancesInWallet()
        {
            // Act.
            var response = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName = this.walletName })
                .GetJsonAsync<WalletBalanceModel>();

            response.AccountsBalances.Should().NotBeEmpty();
            response.AccountsBalances.Should().ContainSingle();

            var accountBalance = response.AccountsBalances.Single();
            accountBalance.HdPath.Should().Be("m/44'/105'/0'");
            accountBalance.Name.Should().Be("account 0");
            accountBalance.CoinType.Should().Be(CoinType.Stratis);
            accountBalance.AmountConfirmed.Should().Be(new Money(142190299995400));
            accountBalance.AmountUnconfirmed.Should().Be(new Money(100000000000));
        }

        [Fact]
        public async Task CheckBalancesWhenNoWalletWithThisNameExists()
        {
            // Arrange.
            string walletName = "no-such-wallet";
            
            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName = walletName })
                .GetJsonAsync<WalletBalanceModel>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No wallet with name '{walletName}' could be found.");
        }

        [Fact]
        public async Task CheckBalancesWhenNoAccountWithThisNameExists()
        {
            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName = this.walletName, accountName = "account 1" })
                .GetJsonAsync<WalletBalanceModel>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No account with the name 'account 1' could be found.");
        }

        [Fact]
        public async Task FundsReceivedByAddress()
        {
            // Arrange.
            string address = "TRCT9QP3ipb6zCvW15yKoEtaU418UaKVE2";
            
            // Act.
            AddressBalanceModel addressBalance = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/received-by-address")
                .SetQueryParams(new { address = address })
                .GetJsonAsync<AddressBalanceModel>();

            addressBalance.Address.Should().Be(address);
            addressBalance.CoinType.Should().Be(CoinType.Stratis);
            addressBalance.AmountConfirmed.Should().Be(new Money(10150100000000));
            addressBalance.AmountUnconfirmed.Should().Be(Money.Zero);
        }

        [Fact]
        public async Task FundsReceivedByAddressWhenNoSuchAddressExists()
        {
            // Arrange.
            string address = "TX725W9ngnnoNuXX6mxvx5iHwS9VEuTa4s";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/received-by-address")
                .SetQueryParams(new { address = address })
                .GetJsonAsync<AddressBalanceModel>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"Address '{address}' not found in wallets.");
        }

        [Fact]
        public async Task CheckMaxBalancesInWallet()
        {
            // Act.
            var balanceResponse = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName = this.walletName })
                .GetJsonAsync<WalletBalanceModel>();

            var maxBalanceResponse = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/maxbalance")
                .SetQueryParams(new { walletName = this.walletName, accountName = "account 0", feetype = "low", allowunconfirmed = true })
                .GetJsonAsync<MaxSpendableAmountModel>();

            var accountBalance = balanceResponse.AccountsBalances.Single();
            var totalBalance = accountBalance.AmountConfirmed + accountBalance.AmountUnconfirmed;

            maxBalanceResponse.MaxSpendableAmount.Should().Be(new Money(24289999986040));
            maxBalanceResponse.Fee.Should().Be(new Money(9360));
        }

        [Fact]
        public async Task CheckMaxBalancesInWalletWhenNoWalletWithThisNameExists()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/maxbalance")
                .SetQueryParams(new { walletName = walletName, accountName = "account 0", feetype = "low", allowunconfirmed = true })
                .GetJsonAsync<MaxSpendableAmountModel>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No wallet with name '{walletName}' could be found.");
        }

        [Fact]
        public async Task GetExtPubKeyWhenNoAccountWithThisNameExists()
        {
            // Arrange.
            string accountName = "account 1222";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/extpubkey")
                .SetQueryParams(new { walletName = this.walletName, accountName = accountName })
                .GetJsonAsync<string>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No account with the name '{accountName}' could be found.");
        }

        [Fact]
        public async Task GetExtPubKeyForAccount()
        {
            // Arrange.
            string accountName = "account 0";

            // Act.
            string extPubKey = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/extpubkey")
                .SetQueryParams(new { walletName = this.walletName, accountName = accountName })
                .GetJsonAsync<string>();

            // Assert.
            extPubKey.Should().Be("xpub6CDG8zbSEN2uGfnYSS9EsizpfmVv9wrBggHyDR4KLAempCXS2FpKL3xSvJwwmS5iEESZCPUuAoMsQvYYbyuTuEEkdrPVkgFBRAEoucFYTfr");
        }

        [Fact]
        public async Task GetAccountsInWallet()
        {
            // Act.
            IEnumerable<string> accountsNames = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName = this.walletName })
                .GetJsonAsync<IEnumerable<string>>();

            // Assert.
            accountsNames.Should().NotBeEmpty();
            
            string firstAccountName = accountsNames.First();
            firstAccountName.Should().Be("account 0");
        }

        [Fact]
        public async Task GetAccountsInWalletWhenNoWalletWithThisNameExists()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName = walletName })
                .GetJsonAsync<string>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No wallet with name '{walletName}' could be found.");
        }

        
        [Fact]
        public async Task GetAddressesInAccount()
        {
            // Act.
            AddressesModel addressesModel = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.walletName, accountName = "account 0" })
                .GetJsonAsync<AddressesModel>();

            // Assert.
            addressesModel.Addresses.Count().Should().Be(50);
            addressesModel.Addresses.Where(a => a.IsUsed).Count().Should().Be(10);
            addressesModel.Addresses.Where(a => a.IsChange).Count().Should().Be(22);
        }

        [Fact]
        public async Task GetAddressesInAccountWhenNoWalletWithThisNameExists()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = walletName, accountName = "account 0" })
                .GetJsonAsync<string>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No wallet with name '{walletName}' could be found.");
        }

        [Fact]
        public async Task GetAddressesInAccountWhenNoAccountWithThisNameExists()
        {
            // Arrange.
            string accountName = "account 122";
            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.walletName, accountName = accountName })
                .GetJsonAsync<string>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No account with the name '{accountName}' could be found.");
        }

        [Fact]
        public async Task GetSingleUnusedAddressesInAccount()
        {
            // Act.
            IEnumerable<string> unusedaddresses = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/unusedAddresses")
                .SetQueryParams(new { walletName = this.walletName, accountName = "account 0", count = 1 })
                .GetJsonAsync<IEnumerable<string>>();

            // Assert.
            unusedaddresses.Count().Should().Be(1);

            string address = unusedaddresses.Single();
            address.Should().Be("TDQAiMyvWZeQxuL9U1BJXt8XrTRMgwjCBe");
        }

        [Fact]
        public async Task GetWalletGeneralInfo()
        {
            // Act.
            WalletGeneralInfoModel generalInfoModel = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/general-info")
                .SetQueryParams(new { name = this.walletName })
                .GetJsonAsync<WalletGeneralInfoModel>();

            // Assert.
            generalInfoModel.ChainTip.Should().NotBeNull();
            generalInfoModel.ConnectedNodes.Should().Be(0);
            generalInfoModel.CreationTime.ToUnixTimeSeconds().Should().Be(1540204793);
            generalInfoModel.IsDecrypted.Should().BeTrue();
            generalInfoModel.Network.Name.Should().Be(new StratisRegTest().Name);
            generalInfoModel.WalletFilePath.Should().Be(this.fixture.WalletFilePath);
        }

        [Fact]
        public async Task GetWalletGeneralInfoWhenNoWalletWithThisNameExists()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/general-info")
                .SetQueryParams(new { name = walletName })
                .GetJsonAsync<WalletGeneralInfoModel>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"No wallet with name '{walletName}' could be found.");
        }

        [Fact]
        public async Task GetWalletFiles()
        {
            // Act.
            WalletFileModel walletFileModel = await $"http://localhost:{this.node.ApiPort}/api"
                .AppendPathSegment("wallet/files")
                .GetJsonAsync<WalletFileModel>();

            // Assert.
            walletFileModel.WalletsPath.Should().Be(Path.GetDirectoryName(this.fixture.WalletFilePath));
            walletFileModel.WalletsFiles.Should().ContainSingle();
            walletFileModel.WalletsFiles.Single().Should().Be(Path.GetFileName(this.fixture.WalletFilePath));
        }
    }
}
