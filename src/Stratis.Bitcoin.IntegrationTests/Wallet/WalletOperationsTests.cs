using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public class WalletOperationsFixture : IDisposable
    {
        private readonly NodeBuilder builder;

        private readonly Network network;

        public CoreNode Node { get; }

        internal readonly string walletWithFundsName = "wallet-with-funds";

        internal readonly string walletWithFundsPassword = "123456";

        internal readonly string addressWithFunds = "TRCT9QP3ipb6zCvW15yKoEtaU418UaKVE2";

        internal readonly string addressWithoutFunds = "TDQAiMyvWZeQxuL9U1BJXt8XrTRMgwjCBe";

        internal readonly string signatureMessage = "This is a test";

        internal readonly string validSignature = "IFpsneU79ikNfeqljDgSwrvdgOyEmrydaib1Xdc/npr7O1s+9GrAzaVOMfvz5x9mq4395JZQfNhSNiUqK0qTW4M=";

        public string WalletWithFundsFilePath { get; }

        public WalletOperationsFixture()
        {
            this.network = new StratisRegTest();
            this.builder = NodeBuilder.Create("WalletOperationsTests");
            CoreNode stratisNode = this.builder.CreateStratisPosNode(this.network).Start();

            string walletsFolderPath = stratisNode.FullNode.DataFolder.WalletPath;
            string filename = $"{this.walletWithFundsName}.wallet.json";
            this.WalletWithFundsFilePath = Path.Combine(walletsFolderPath, filename);
            File.Copy(Path.Combine("Wallet", "Data", filename), this.WalletWithFundsFilePath, true);

            // Stop the wallet sync manager to prevent it from rewinding the wallet.
            stratisNode.FullNode.NodeService<IWalletSyncManager>().Stop();

            // Prevent wallet transactions with non-consensus blocks from being omitted.
            ((WalletManager)stratisNode.FullNode.NodeService<IWalletManager>()).ExcludeTransactionsFromWalletImports = false;

            // Ask the server to load the wallet to its repository.
            var result = $"http://localhost:{stratisNode.ApiPort}/api".AppendPathSegment("wallet/load").PostJsonAsync(new WalletLoadRequest
            {
                Name = this.walletWithFundsName,
                Password = "123456"
            }).Result;

            this.Node = stratisNode;
        }

        /// <summary>
        /// Create a unique wallet name as wallets with the same name can't be loaded by the same node.
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

    /// <summary>
    /// This class contains tests that can all be run with a single node.
    /// </summary>
    public class WalletOperationsTests : IClassFixture<WalletOperationsFixture>
    {
        private readonly WalletOperationsFixture fixture;

        public WalletOperationsTests(WalletOperationsFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async Task GetMnemonicWithDefaultParametersAsync()
        {
            // Act.
            var mnemonic = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/mnemonic").GetStringAsync();

            // Assert.
            mnemonic.Split(" ").Length.Should().Be(12);
            Wordlist.AutoDetectLanguage(mnemonic).Should().Be(Language.English);
        }

        [Fact]
        public async Task GetMnemonicWith24FrenchWordsAsync()
        {
            // Act.
            var mnemonic = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/mnemonic")
                .SetQueryParams(new { language = "French", wordCount = 24 }).
                GetStringAsync();

            // Assert.
            mnemonic.Split(" ").Length.Should().Be(24);
            Wordlist.AutoDetectLanguage(mnemonic).Should().Be(Language.French);
        }

        [Fact]
        public async Task GetMnemonicWithUnknownLanguageFailsAsync()
        {
            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                                .AppendPathSegment("wallet/mnemonic")
                                .SetQueryParams(new { language = "Klingon", wordCount = 24 })
                                .GetAsync();

            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            // Assert.
            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be("Invalid language 'Klingon'. Choices are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional.");
        }

        [Fact]
        public async Task CreateWalletWithoutMnemonicAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "",
                Password = "123456"
            }).ReceiveString();

            // Assert.

            // Check the mnemonic returned.
            response.Split(" ").Length.Should().Be(12);
            Wordlist.AutoDetectLanguage(response).Should().Be(Language.English);

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check a wallet file has been created.
            string walletFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string walletPath = Path.Combine(walletFolderPath, $"{walletName}.wallet.json");
            File.Exists(walletPath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet wallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletPath));
            wallet.IsExtPubKeyWallet.Should().BeFalse();
            wallet.ChainCode.Should().NotBeNullOrEmpty();
            wallet.EncryptedSeed.Should().NotBeNullOrEmpty();
            wallet.Name.Should().Be(walletName);
            wallet.Network.Should().Be(this.fixture.Node.FullNode.Network);

            // Check only one account is created.
            wallet.GetAccounts().Should().ContainSingle();

            // Check the created account.
            HdAccount account = wallet.GetAccounts().Single();
            account.Name.Should().Be("account 0");
            account.ExternalAddresses.Count().Should().Be(20);
            account.InternalAddresses.Count().Should().Be(20);
            account.Index.Should().Be(0);
            account.ExtendedPubKey.Should().NotBeNullOrEmpty();
            account.GetCoinType().Should().Be(CoinType.Stratis);
            account.HdPath.Should().Be("m/44'/105'/0'");
        }

        [Fact]
        public async Task CreateWalletWith12WordsMnemonicAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();
            string mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString();

            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "",
                Password = "123456",
                Mnemonic = mnemonic
            }).ReceiveString();

            // Assert.

            // Check the mnemonic returned.
            response = response.Replace("\"", "");
            response.Split(" ").Length.Should().Be(12);
            Wordlist.AutoDetectLanguage(response).Should().Be(Language.English);
            response.Should().Be(mnemonic);

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check a wallet file has been created.
            string walletFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string walletPath = Path.Combine(walletFolderPath, $"{walletName}.wallet.json");
            File.Exists(walletPath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet wallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletPath));
            wallet.IsExtPubKeyWallet.Should().BeFalse();
            wallet.ChainCode.Should().NotBeNullOrEmpty();
            wallet.EncryptedSeed.Should().NotBeNullOrEmpty();
            wallet.Name.Should().Be(walletName);
            wallet.Network.Should().Be(this.fixture.Node.FullNode.Network);

            // Check only one account is created.
            wallet.GetAccounts().Should().ContainSingle();

            // Check the created account.
            HdAccount account = wallet.GetAccounts().Single();
            account.Name.Should().Be("account 0");
            account.ExternalAddresses.Count().Should().Be(20);
            account.InternalAddresses.Count().Should().Be(20);
            account.Index.Should().Be(0);
            account.ExtendedPubKey.Should().NotBeNullOrEmpty();
            account.GetCoinType().Should().Be(CoinType.Stratis);
            account.HdPath.Should().Be("m/44'/105'/0'");
        }

        [Fact]
        public async Task CreateWalletWith12WordsChineseMnemonicAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();
            string mnemonic = new Mnemonic(Wordlist.ChineseTraditional, WordCount.Twelve).ToString();

            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "",
                Password = "123456",
                Mnemonic = mnemonic
            }).ReceiveString();

            // Assert.

            // Check the mnemonic returned.
            response = response.Replace("\"", "");
            response.Split(" ").Length.Should().Be(12);
            Language detectedLanguage = Wordlist.AutoDetectLanguage(response);

            // Relax the test due to the potential language ambiguity of the words returned.
            detectedLanguage.Should().Match(p => p.Equals(Language.ChineseTraditional) || p.Equals(Language.ChineseSimplified));

            response.Should().Be(mnemonic);

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check a wallet file has been created.
            string walletFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string walletPath = Path.Combine(walletFolderPath, $"{walletName}.wallet.json");
            File.Exists(walletPath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet wallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletPath));
            wallet.IsExtPubKeyWallet.Should().BeFalse();
            wallet.ChainCode.Should().NotBeNullOrEmpty();
            wallet.EncryptedSeed.Should().NotBeNullOrEmpty();
            wallet.Name.Should().Be(walletName);
            wallet.Network.Should().Be(this.fixture.Node.FullNode.Network);

            // Check only one account is created.
            wallet.GetAccounts().Should().ContainSingle();

            // Check the created account.
            HdAccount account = wallet.GetAccounts().Single();
            account.Name.Should().Be("account 0");
            account.ExternalAddresses.Count().Should().Be(20);
            account.InternalAddresses.Count().Should().Be(20);
            account.Index.Should().Be(0);
            account.ExtendedPubKey.Should().NotBeNullOrEmpty();
            account.GetCoinType().Should().Be(CoinType.Stratis);
            account.HdPath.Should().Be("m/44'/105'/0'");
        }

        [Fact]
        public async Task CreateWalletWith24WordsMnemonicAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();
            string mnemonic = new Mnemonic(Wordlist.English, WordCount.TwentyFour).ToString();

            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "",
                Password = "123456",
                Mnemonic = mnemonic
            }).ReceiveString();

            // Assert.

            // Check the mnemonic returned.
            response = response.Replace("\"", "");
            response.Split(" ").Length.Should().Be(24);
            Wordlist.AutoDetectLanguage(response).Should().Be(Language.English);
            response.Should().Be(mnemonic);

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check a wallet file has been created.
            string walletFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string walletPath = Path.Combine(walletFolderPath, $"{walletName}.wallet.json");
            File.Exists(walletPath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet wallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletPath));
            wallet.IsExtPubKeyWallet.Should().BeFalse();
            wallet.ChainCode.Should().NotBeNullOrEmpty();
            wallet.EncryptedSeed.Should().NotBeNullOrEmpty();
            wallet.Name.Should().Be(walletName);
            wallet.Network.Should().Be(this.fixture.Node.FullNode.Network);

            // Check only one account is created.
            wallet.GetAccounts().Should().ContainSingle();

            // Check the created account.
            HdAccount account = wallet.GetAccounts().Single();
            account.Name.Should().Be("account 0");
            account.ExternalAddresses.Count().Should().Be(20);
            account.InternalAddresses.Count().Should().Be(20);
            account.Index.Should().Be(0);
            account.ExtendedPubKey.Should().NotBeNullOrEmpty();
            account.GetCoinType().Should().Be(CoinType.Stratis);
            account.HdPath.Should().Be("m/44'/105'/0'");
        }

        [Fact]
        public async Task CreateWalletWithPassphraseAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Password = "123456"
            }).ReceiveString();

            // Assert.

            // Check the mnemonic returned.
            response = response.Replace("\"", "");
            response.Split(" ").Length.Should().Be(12);
            Wordlist.AutoDetectLanguage(response).Should().Be(Language.English);

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check a wallet file has been created.
            string walletFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string walletPath = Path.Combine(walletFolderPath, $"{walletName}.wallet.json");
            File.Exists(walletPath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet wallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletPath));
            wallet.IsExtPubKeyWallet.Should().BeFalse();
            wallet.ChainCode.Should().NotBeNullOrEmpty();
            wallet.EncryptedSeed.Should().NotBeNullOrEmpty();
            wallet.Name.Should().Be(walletName);
            wallet.Network.Should().Be(this.fixture.Node.FullNode.Network);

            // Check only one account is created.
            wallet.GetAccounts().Should().ContainSingle();

            // Check the created account.
            HdAccount account = wallet.GetAccounts().Single();
            account.Name.Should().Be("account 0");
            account.ExternalAddresses.Count().Should().Be(20);
            account.InternalAddresses.Count().Should().Be(20);
            account.Index.Should().Be(0);
            account.ExtendedPubKey.Should().NotBeNullOrEmpty();
            account.GetCoinType().Should().Be(CoinType.Stratis);
            account.HdPath.Should().Be("m/44'/105'/0'");
        }

        [Fact]
        public async Task CreateWalletWithoutPasswordAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Password = ""
            }).ReceiveString();

            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            // Assert.
            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be("A password is required.");
        }

        [Fact]
        public async Task CompareWalletsCreatedWithAndWithoutPassphraseAsync()
        {
            // Arrange.
            string walletWithPassphraseName = "wallet-with-passphrase";
            string walletWithoutPassphraseName = "wallet-without-passphrase";
            string walletsFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;

            // Act.
            var mnemonic = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletWithPassphraseName,
                Passphrase = "passphrase",
                Password = "123456"
            }).ReceiveString();

            var mnemonic2 = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletWithoutPassphraseName,
                Passphrase = "",
                Password = "123456"
            }).ReceiveString();

            // Assert.

            // Check the mnemonics returned.
            mnemonic = mnemonic.Replace("\"", "");
            mnemonic2 = mnemonic2.Replace("\"", "");
            mnemonic.Split(" ").Length.Should().Be(12);
            mnemonic2.Split(" ").Length.Should().Be(12);
            Wordlist.AutoDetectLanguage(mnemonic).Should().Be(Language.English);
            Wordlist.AutoDetectLanguage(mnemonic2).Should().Be(Language.English);
            mnemonic2.Should().NotBe(mnemonic);

            // Check a wallet files have been created.
            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletWithPassphraseName);
            walletManager.SaveWallet(walletWithoutPassphraseName);

            string walletWithPassphrasePath = Path.Combine(walletsFolderPath, $"{walletWithPassphraseName}.wallet.json");
            File.Exists(walletWithPassphrasePath).Should().BeTrue();

            string walletWithoutPassphrasePath = Path.Combine(walletsFolderPath, $"{walletWithoutPassphraseName}.wallet.json");
            File.Exists(walletWithoutPassphrasePath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet walletWithPassphrase = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletWithPassphrasePath));
            walletWithPassphrase.IsExtPubKeyWallet.Should().BeFalse();
            walletWithPassphrase.ChainCode.Should().NotBeNullOrEmpty();
            walletWithPassphrase.EncryptedSeed.Should().NotBeNullOrEmpty();

            Features.Wallet.Wallet walletWithoutPassphrase = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletWithoutPassphrasePath));
            walletWithoutPassphrase.IsExtPubKeyWallet.Should().BeFalse();
            walletWithoutPassphrase.ChainCode.Should().NotBeNullOrEmpty();
            walletWithoutPassphrase.EncryptedSeed.Should().NotBeNullOrEmpty();

            walletWithoutPassphrase.EncryptedSeed.Should().NotBe(walletWithPassphrase.EncryptedSeed);
            walletWithoutPassphrase.ChainCode.Should().NotBeEquivalentTo(walletWithPassphrase.ChainCode);
            walletWithoutPassphrase.AccountsRoot.First().Accounts.First().ExtendedPubKey.Should().NotBe(walletWithPassphrase.AccountsRoot.First().Accounts.First().ExtendedPubKey);
        }

        [Fact]
        public async Task CreateWalletsWithSameMnemonicPassphraseCombinationFailsAsync()
        {
            // Arrange.
            string firstWalletName = this.fixture.GetUniqueWalletName();
            string secondWalletName = this.fixture.GetUniqueWalletName();
            string walletsFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;

            // Act.
            var mnemonic = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = firstWalletName,
                Passphrase = "passphrase",
                Password = "123456"
            }).ReceiveString();

            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = secondWalletName,
                Passphrase = "passphrase",
                Password = "123456",
                Mnemonic = mnemonic.Replace("\"", "")
            }).ReceiveString();

            // Assert.

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(firstWalletName);
            Assert.Throws<WalletException>(() => walletManager.SaveWallet(secondWalletName));

            // Check only one wallet has been created.
            string firstWalletPath = Path.Combine(walletsFolderPath, $"{firstWalletName}.wallet.json");
            File.Exists(firstWalletPath).Should().BeTrue();

            string secondWalletPath = Path.Combine(walletsFolderPath, $"{secondWalletName}.wallet.json");
            File.Exists(secondWalletPath).Should().BeFalse();

            // Check the error message.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Contain("Cannot create this wallet as a wallet with the same private key already exists.");
        }

        [Fact]
        public async Task CreateWalletsWithSameNameFailsAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();
            string walletsFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;

            // Act.
            var mnemonic = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Password = "123456"
            }).ReceiveString();

            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Password = "password"
            }).ReceiveString();

            // Assert.

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check only one wallet has been created.
            string firstWalletPath = Path.Combine(walletsFolderPath, $"{walletName}.wallet.json");
            File.Exists(firstWalletPath).Should().BeTrue();

            // Check the error message.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Contain($"Wallet with name '{walletName}' already exists.");
        }

        [Fact]
        public async Task LoadNonExistingWalletAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/load").PostJsonAsync(new WalletLoadRequest
            {
                Name = walletName,
                Password = "password"
            });

            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            Assert.Throws<WalletException>(() => walletManager.SaveWallet(walletName));

            // Assert.
            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be("This wallet was not found at the specified location.");
        }

        [Fact]
        public async Task LoadWalletAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();
            string walletsFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string importWalletPath = Path.Combine("Wallet", "Data", "test.wallet.json");

            Features.Wallet.Wallet importedWallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(importWalletPath));
            importedWallet.Name = walletName;
            File.WriteAllText(Path.Combine(walletsFolderPath, $"{walletName}.wallet.json"), JsonConvert.SerializeObject(importedWallet, Formatting.Indented));

            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/load").PostJsonAsync(new WalletLoadRequest
            {
                Name = walletName,
                Password = "123456"
            });

            // Assert.
            response.StatusCode = HttpStatusCode.Accepted;

            // Check the wallet is loaded.
            var getAccountsResponse = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName })
                .GetJsonAsync<IEnumerable<string>>();

            getAccountsResponse.First().Should().Be("account 0");
        }

        [Fact]
        public async Task LoadWalletWithWrongPasswordAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();
            string walletsFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string importWalletPath = Path.Combine("Wallet", "Data", "test.wallet.json");

            Features.Wallet.Wallet importedWallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(importWalletPath));
            importedWallet.Name = walletName;
            File.WriteAllText(Path.Combine(walletsFolderPath, $"{walletName}.wallet.json"), JsonConvert.SerializeObject(importedWallet, Formatting.Indented));

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/load").PostJsonAsync(new WalletLoadRequest
            {
                Name = walletName,
                Password = "wrongpassword"
            });

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be("Wrong password, please try again.");

            // Check the wallet hasn't been loaded.
            Func<Task> getAccounts = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName })
                .GetJsonAsync<IEnumerable<string>>();

            var exception2 = getAccounts.Should().Throw<FlurlHttpException>().Which;
            var response2 = exception2.Call.Response;

            // Assert.
            ErrorResponse errorResponse2 = JsonConvert.DeserializeObject<ErrorResponse>(await response2.Content.ReadAsStringAsync());
            List<ErrorModel> errors2 = errorResponse2.Errors;

            response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors2.Should().ContainSingle();
            errors2.First().Message.Should().Be($"No wallet with name '{walletName}' could be found.");
        }

        [Fact]
        public async Task RecoverWalletWithWrongNumberOfWordsInMnemonicAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/recover").PostJsonAsync(new WalletRecoveryRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Password = "password",
                Mnemonic = "pumpkin census skill noise write vicious plastic carpet vault"
            }).ReceiveString();

            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            // Assert.
            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be("Word count should be equals to 12,15,18,21 or 24");
        }

        [Fact]
        public async Task RecoverWalletWithoutMnemonicAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/recover").PostJsonAsync(new WalletRecoveryRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Password = "password"
            }).ReceiveString();

            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            // Assert.
            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be("A mnemonic is required.");
        }

        [Fact]
        public async Task RecoverWalletWithoutPasswordAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/recover").PostJsonAsync(new WalletRecoveryRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Mnemonic = new Mnemonic(Wordlist.Japanese, WordCount.Twelve).ToString()
            }).ReceiveString();

            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            // Assert.
            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be("A password is required.");
        }

        [Fact]
        public async Task RecoverWalletWithPassphraseAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/recover").PostJsonAsync(new WalletRecoveryRequest
            {
                Name = walletName,
                Passphrase = "passphrase",
                Password = "123456",
                Mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString(),
                CreationDate = DateTime.Parse("2018-1-1")
            });

            // Assert.

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check a wallet file has been created.
            string walletFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string walletPath = Path.Combine(walletFolderPath, $"{walletName}.wallet.json");
            File.Exists(walletPath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet wallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletPath));
            wallet.IsExtPubKeyWallet.Should().BeFalse();
            wallet.ChainCode.Should().NotBeNullOrEmpty();
            wallet.EncryptedSeed.Should().NotBeNullOrEmpty();
            wallet.Name.Should().Be(walletName);
            wallet.Network.Should().Be(this.fixture.Node.FullNode.Network);

            // Check only one account is created.
            wallet.GetAccounts().Should().ContainSingle();

            // Check the created account.
            HdAccount account = wallet.GetAccounts().Single();
            account.Name.Should().Be("account 0");
            account.ExternalAddresses.Count().Should().Be(20);
            account.InternalAddresses.Count().Should().Be(20);
            account.Index.Should().Be(0);
            account.ExtendedPubKey.Should().NotBeNullOrEmpty();
            account.GetCoinType().Should().Be(CoinType.Stratis);
            account.HdPath.Should().Be("m/44'/105'/0'");
        }

        [Fact]
        public async Task RecoverWalletWithoutPassphraseAsync()
        {
            // Arrange.
            string walletName = this.fixture.GetUniqueWalletName();

            // Act.
            await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/recover").PostJsonAsync(new WalletRecoveryRequest
            {
                Name = walletName,
                Passphrase = "",
                Password = "123456",
                Mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString(),
                CreationDate = DateTime.Parse("2018-1-1")
            });

            // Assert.

            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(walletName);

            // Check a wallet file has been created.
            string walletFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;
            string walletPath = Path.Combine(walletFolderPath, $"{walletName}.wallet.json");
            File.Exists(walletPath).Should().BeTrue();

            // Check the wallet.
            Features.Wallet.Wallet wallet = JsonConvert.DeserializeObject<Features.Wallet.Wallet>(File.ReadAllText(walletPath));
            wallet.IsExtPubKeyWallet.Should().BeFalse();
            wallet.ChainCode.Should().NotBeNullOrEmpty();
            wallet.EncryptedSeed.Should().NotBeNullOrEmpty();
            wallet.Name.Should().Be(walletName);
            wallet.Network.Should().Be(this.fixture.Node.FullNode.Network);

            // Check only one account is created.
            wallet.GetAccounts().Should().ContainSingle();

            // Check the created account.
            HdAccount account = wallet.GetAccounts().Single();
            account.Name.Should().Be("account 0");
            account.ExternalAddresses.Count().Should().Be(20);
            account.InternalAddresses.Count().Should().Be(20);
            account.Index.Should().Be(0);
            account.ExtendedPubKey.Should().NotBeNullOrEmpty();
            account.GetCoinType().Should().Be(CoinType.Stratis);
            account.HdPath.Should().Be("m/44'/105'/0'");
        }

        [Fact]
        public async Task RecoverWalletWithSameMnemonicPassphraseAsExistingWalletFailsAsync()
        {
            // Arrange.
            string firstWalletName = this.fixture.GetUniqueWalletName();
            string secondWalletName = this.fixture.GetUniqueWalletName();
            string walletsFolderPath = this.fixture.Node.FullNode.DataFolder.WalletPath;

            // Act.
            var mnemonic = await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/create").PostJsonAsync(new WalletCreationRequest
            {
                Name = firstWalletName,
                Passphrase = "passphrase",
                Password = "123456"
            }).ReceiveString();

            mnemonic = mnemonic.Replace("\"", "");

            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api".AppendPathSegment("wallet/recover").PostJsonAsync(new WalletRecoveryRequest
            {
                Name = secondWalletName,
                Passphrase = "passphrase",
                Password = "123456",
                Mnemonic = mnemonic,
                CreationDate = DateTime.Parse("2018-1-1")
            }).ReceiveString();

            // Assert.
            var walletManager = this.fixture.Node.FullNode.NodeService<IWalletManager>();
            walletManager.SaveWallet(firstWalletName);
            Assert.Throws<WalletException>(() => walletManager.SaveWallet(secondWalletName));

            // Check only one wallet has been created.
            string firstWalletPath = Path.Combine(walletsFolderPath, $"{firstWalletName}.wallet.json");
            File.Exists(firstWalletPath).Should().BeTrue();

            string secondWalletPath = Path.Combine(walletsFolderPath, $"{secondWalletName}.wallet.json");
            File.Exists(secondWalletPath).Should().BeFalse();

            // Check the error message.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Contain("Cannot create this wallet as a wallet with the same private key already exists.");
        }

        [Fact]
        public async Task CheckBalancesInWalletAsync()
        {
            // Act.
            var response = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName })
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
        public async Task CheckBalancesWhenNoWalletWithThisNameExistsAsync()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName })
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
        public async Task CheckBalancesWhenNoAccountWithThisNameExistsAsync()
        {
            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName, accountName = "account 1" })
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
        public async Task FundsReceivedByAddressAsync()
        {
            // TODO: These tests are all in the situation where the wallet is ahead of the ChainIndexer.
            // They don't make much sense. How could the chain be at height 0 but we have new "Money(10150100000000)" Confirmed???
            // The tests should be checking a real situation, like where the wallet is at block N and the ChainIndexer is also at block N (or higher).

            // Arrange.
            string address = "TRCT9QP3ipb6zCvW15yKoEtaU418UaKVE2";

            // Act.
            AddressBalanceModel addressBalance = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/received-by-address")
                .SetQueryParams(new { address })
                .GetJsonAsync<AddressBalanceModel>();

            addressBalance.Address.Should().Be(address);
            addressBalance.CoinType.Should().Be(CoinType.Stratis);
            addressBalance.AmountConfirmed.Should().Be(new Money(10150100000000));
            addressBalance.AmountUnconfirmed.Should().Be(Money.Zero);

            // The total that we have received to this address, apart from coinbases/staking which require maturity.
            addressBalance.SpendableAmount.Should().Be(new Money(1500, MoneyUnit.BTC));
        }

        [Fact]
        public async Task FundsReceivedByAddressWhenNoSuchAddressExistsAsync()
        {
            // Arrange.
            string address = "TX725W9ngnnoNuXX6mxvx5iHwS9VEuTa4s";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/received-by-address")
                .SetQueryParams(new { address })
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
        public async Task CheckMaxBalancesInWalletAsync()
        {
            // Act.
            var balanceResponse = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/balance")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName })
                .GetJsonAsync<WalletBalanceModel>();

            var maxBalanceResponse = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/maxbalance")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName, accountName = "account 0", feetype = "low", allowunconfirmed = true })
                .GetJsonAsync<MaxSpendableAmountModel>();

            var accountBalance = balanceResponse.AccountsBalances.Single();
            var totalBalance = accountBalance.AmountConfirmed + accountBalance.AmountUnconfirmed;

            maxBalanceResponse.MaxSpendableAmount.Should().Be(new Money(24289999986040));
            maxBalanceResponse.Fee.Should().Be(new Money(9360));
        }

        [Fact]
        public async Task CheckMaxBalancesInWalletWhenNoWalletWithThisNameExistsAsync()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/maxbalance")
                .SetQueryParams(new { walletName, accountName = "account 0", feetype = "low", allowunconfirmed = true })
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
        public async Task GetExtPubKeyWhenNoAccountWithThisNameExistsAsync()
        {
            // Arrange.
            string accountName = "account 1222";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/extpubkey")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName, accountName })
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
        public async Task GetExtPubKeyForAccountAsync()
        {
            // Arrange.
            string accountName = "account 0";

            // Act.
            string extPubKey = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/extpubkey")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName, accountName })
                .GetJsonAsync<string>();

            // Assert.
            extPubKey.Should().Be("xpub6CDG8zbSEN2uGfnYSS9EsizpfmVv9wrBggHyDR4KLAempCXS2FpKL3xSvJwwmS5iEESZCPUuAoMsQvYYbyuTuEEkdrPVkgFBRAEoucFYTfr");
        }

        [Fact]
        public async Task GetAccountsInWalletAsync()
        {
            // Act.
            IEnumerable<string> accountsNames = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName })
                .GetJsonAsync<IEnumerable<string>>();

            // Assert.
            accountsNames.Should().NotBeEmpty();

            string firstAccountName = accountsNames.First();
            firstAccountName.Should().Be("account 0");
        }

        [Fact]
        public async Task GetAccountsInWalletWhenNoWalletWithThisNameExistsAsync()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/accounts")
                .SetQueryParams(new { walletName })
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
        public async Task GetAddressesInAccountAsync()
        {
            // Act.
            AddressesModel addressesModel = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName, accountName = "account 0" })
                .GetJsonAsync<AddressesModel>();

            // Assert.
            addressesModel.Addresses.Count().Should().Be(50);
            addressesModel.Addresses.Where(a => a.IsUsed).Count().Should().Be(10);
            addressesModel.Addresses.Where(a => a.IsChange).Count().Should().Be(22);
        }

        [Fact]
        public async Task GetAddressesInAccountWhenNoWalletWithThisNameExistsAsync()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName, accountName = "account 0" })
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
        public async Task GetAddressesInAccountWhenNoAccountWithThisNameExistsAsync()
        {
            // Arrange.
            string accountName = "account 122";
            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/addresses")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName, accountName })
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
        public async Task GetSingleUnusedAddressesInAccountAsync()
        {
            // Act.
            IEnumerable<string> unusedaddresses = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/unusedAddresses")
                .SetQueryParams(new { walletName = this.fixture.walletWithFundsName, accountName = "account 0", count = 1 })
                .GetJsonAsync<IEnumerable<string>>();

            // Assert.
            unusedaddresses.Count().Should().Be(1);

            string address = unusedaddresses.Single();
            address.Should().Be("TDQAiMyvWZeQxuL9U1BJXt8XrTRMgwjCBe");
        }

        [Fact]
        public async Task GetWalletGeneralInfoAsync()
        {
            // Act.
            WalletGeneralInfoModel generalInfoModel = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/general-info")
                .SetQueryParams(new { name = this.fixture.walletWithFundsName })
                .GetJsonAsync<WalletGeneralInfoModel>();

            // Assert.
            generalInfoModel.ChainTip.Should().NotBeNull();
            generalInfoModel.ConnectedNodes.Should().Be(0);
            generalInfoModel.CreationTime.ToUnixTimeSeconds().Should().Be(1540204793);
            generalInfoModel.IsDecrypted.Should().BeTrue();
            generalInfoModel.Network.Name.Should().Be(new StratisRegTest().Name);
            //generalInfoModel.WalletFilePath.Should().Be(this.fixture.WalletWithFundsFilePath);
        }

        [Fact]
        public async Task BuildTransactionFromWalletAsync()
        {
            // Arrange.
            var address = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();

            // Act.
            WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new BuildTransactionRequest
                {
                    WalletName = this.fixture.walletWithFundsName,
                    AccountName = "account 0",
                    FeeType = "low",
                    Password = "123456",
                    ShuffleOutputs = true,
                    AllowUnconfirmed = true,
                    Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = address, Amount = "1000" } }
                })
                .ReceiveJson<WalletBuildTransactionModel>();

            // Assert.
            buildTransactionModel.Fee.Should().Be(new Money(10000));

            Transaction trx = this.fixture.Node.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);
            trx.Outputs.Should().Contain(o => o.Value == Money.COIN * 1000 && o.ScriptPubKey == BitcoinAddress.Create(address, this.fixture.Node.FullNode.Network).ScriptPubKey);
        }

        [Fact]
        public async Task BuildTransactionWithSelectedInputsAsync()
        {
            // Arrange.
            var address = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();

            // Act.
            WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new BuildTransactionRequest
                {
                    WalletName = this.fixture.walletWithFundsName,
                    AccountName = "account 0",
                    FeeType = "low",
                    Password = "123456",
                    ShuffleOutputs = true,
                    AllowUnconfirmed = true,
                    Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = address, Amount = "1000" } },
                    Outpoints = new List<OutpointRequest>
                    {
                        new OutpointRequest{ Index = 1, TransactionId = "4f1766c2dca4bb96bb7282b4eef113c0956f1ad50ba1a205bec50c7770cac2d5" }, //150000000000
                        new OutpointRequest{ Index = 1, TransactionId = "a40cf5f3c20cf265f5e1a360c7c984688b191993792e7a9cd6227c952b840710" }, //19000000000000
                        new OutpointRequest{ Index = 0, TransactionId = "8b2e57f8959272d357682ede444244d9831cb47e9c936ea9452657a5633a53b5" }, //39999997700
                        new OutpointRequest{ Index = 1, TransactionId = "385ed3fd641f2c33f7c03b9698e69ff03beea90f1e1e0a5943b1a0f4fd29ed97" }, //2500000000000
                    }
                })
                .ReceiveJson<WalletBuildTransactionModel>();

            // Assert.
            buildTransactionModel.Fee.Should().Be(new Money(10000));

            Transaction trx = this.fixture.Node.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);
            trx.Outputs.Should().Contain(o => o.Value == Money.COIN * 1000 && o.ScriptPubKey == BitcoinAddress.Create(address, this.fixture.Node.FullNode.Network).ScriptPubKey);
            trx.Inputs.Should().HaveCount(4);
            trx.Inputs.Should().Contain(i => i.PrevOut == new OutPoint(uint256.Parse("4f1766c2dca4bb96bb7282b4eef113c0956f1ad50ba1a205bec50c7770cac2d5"), 1));
            trx.Inputs.Should().Contain(i => i.PrevOut == new OutPoint(uint256.Parse("a40cf5f3c20cf265f5e1a360c7c984688b191993792e7a9cd6227c952b840710"), 1));
            trx.Inputs.Should().Contain(i => i.PrevOut == new OutPoint(uint256.Parse("8b2e57f8959272d357682ede444244d9831cb47e9c936ea9452657a5633a53b5"), 0));
            trx.Inputs.Should().Contain(i => i.PrevOut == new OutPoint(uint256.Parse("385ed3fd641f2c33f7c03b9698e69ff03beea90f1e1e0a5943b1a0f4fd29ed97"), 1));
        }

        [Fact]
        public async Task BuildTransactionWithMultipleRecipientsAsync()
        {
            // Arrange.
            var address1 = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();
            var address2 = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();

            // Act.
            WalletBuildTransactionModel buildTransactionModel = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new BuildTransactionRequest
                {
                    WalletName = this.fixture.walletWithFundsName,
                    AccountName = "account 0",
                    FeeType = "low",
                    Password = "123456",
                    ShuffleOutputs = true,
                    AllowUnconfirmed = true,
                    Recipients = new List<RecipientModel> {
                        new RecipientModel { DestinationAddress = address1, Amount = "1000" },
                        new RecipientModel { DestinationAddress = address2, Amount = "5000" }
                    }
                })
                .ReceiveJson<WalletBuildTransactionModel>();

            // Assert.
            buildTransactionModel.Fee.Should().Be(new Money(10000));

            Transaction trx = this.fixture.Node.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction(buildTransactionModel.Hex);
            trx.Outputs.Should().Contain(o => o.Value == Money.COIN * 1000 && o.ScriptPubKey == BitcoinAddress.Create(address1, this.fixture.Node.FullNode.Network).ScriptPubKey);
            trx.Outputs.Should().Contain(o => o.Value == Money.COIN * 5000 && o.ScriptPubKey == BitcoinAddress.Create(address2, this.fixture.Node.FullNode.Network).ScriptPubKey);
        }

        [Fact]
        public async Task BuildTransactionFailsWhenUsingFeeAmountAndFeeTypeAsync()
        {
            // Arrange.
            var address = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new BuildTransactionRequest
                {
                    WalletName = this.fixture.walletWithFundsName,
                    AccountName = "account 0",
                    FeeAmount = "1200",
                    FeeType = "low",
                    Password = "123456",
                    ShuffleOutputs = true,
                    AllowUnconfirmed = true,
                    Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = address, Amount = "1000" } }
                })
                .ReceiveJson<WalletBuildTransactionModel>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"The query parameters '{nameof(BuildTransactionRequest.FeeAmount)}' and '{nameof(BuildTransactionRequest.FeeType)}' cannot be set at the same time. " +
                    $"Please use '{nameof(BuildTransactionRequest.FeeAmount)}' if you'd like to set the fee manually, or '{nameof(BuildTransactionRequest.FeeType)}' if you want the wallet to calculate it for you.");
        }

        [Fact]
        public async Task BuildTransactionFailsWhenNoFeeMethodSpecifiedAsync()
        {
            // Arrange.
            var address = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new BuildTransactionRequest
                {
                    WalletName = this.fixture.walletWithFundsName,
                    AccountName = "account 0",
                    Password = "123456",
                    ShuffleOutputs = true,
                    AllowUnconfirmed = true,
                    Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = address, Amount = "1000" } }
                })
                .ReceiveJson<WalletBuildTransactionModel>();

            // Assert.
            var exception = act.Should().Throw<FlurlHttpException>().Which;
            var response = exception.Call.Response;

            ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
            List<ErrorModel> errors = errorResponse.Errors;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errors.Should().ContainSingle();
            errors.First().Message.Should().Be($"One of parameters '{nameof(BuildTransactionRequest.FeeAmount)}' and '{nameof(BuildTransactionRequest.FeeType)}' is required. " +
                    $"Please use '{nameof(BuildTransactionRequest.FeeAmount)}' if you'd like to set the fee manually, or '{nameof(BuildTransactionRequest.FeeType)}' if you want the wallet to calculate it for you.");
        }

        // TODO: I don't think this test is valid because it depends on coinselection algoritm.
        // would be better to pre-select Outpoints so that we know ahead the expected fee
        [Fact]
        public async Task EstimateFeeAsyncAsync()
        {
            // Arrange.
            var address1 = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();
            var address2 = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();

            // Act.
            Money act = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                        .AppendPathSegment("wallet/estimate-txfee")
                        .PostJsonAsync(new TxFeeEstimateRequest
                        {
                            WalletName = this.fixture.walletWithFundsName,
                            AccountName = "account 0",
                            Recipients = new List<RecipientModel>
                            {
                                new RecipientModel { DestinationAddress = address1, Amount = "240000" },
                                new RecipientModel { DestinationAddress = address2, Amount = "1000" },
                            },
                            FeeType = FeeType.Low.ToString(),
                            AllowUnconfirmed = true,
                            ShuffleOutputs = true
                        })
                        .ReceiveJson<Money>();

            // Assert.
            act.Should().Be(new Money(10000));
        }

        // TODO: I don't think this test is valid because it depends on coinselection algoritm.
        // would be better to pre-select Outpoints so that we know ahead the expected fee
        [Fact]
        public async Task EstimateFeeWithOpReturnAsync()
        {
            // Arrange.
            var address1 = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();
            var address2 = new Key().PubKey.GetAddress(this.fixture.Node.FullNode.Network).ToString();

            // Act.
            Money act = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                        .AppendPathSegment("wallet/estimate-txfee")
                        .PostJsonAsync(new TxFeeEstimateRequest
                        {
                            WalletName = this.fixture.walletWithFundsName,
                            AccountName = "account 0",
                            Recipients = new List<RecipientModel>
                            {
                                new RecipientModel { DestinationAddress = address1, Amount = "240000" },
                                new RecipientModel { DestinationAddress = address2, Amount = "2000" },
                            },
                            FeeType = FeeType.Low.ToString(),
                            OpReturnData = "always something interesting to say here",
                            OpReturnAmount = "1",
                            AllowUnconfirmed = true,
                            ShuffleOutputs = true
                        })
                        .ReceiveJson<Money>();

            // Assert.
            act.Should().Be(new Money(10000));
        }

        [Fact]
        public async Task GetWalletGeneralInfoWhenNoWalletWithThisNameExistsAsync()
        {
            // Arrange.
            string walletName = "no-such-wallet";

            // Act.
            Func<Task> act = async () => await $"http://localhost:{this.fixture.Node.ApiPort}/api"
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
        public async Task ListWalletsAsync()
        {
            // Act.
            WalletInfoModel walletFileModel = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/list-wallets")
                .GetJsonAsync<WalletInfoModel>();

            // Assert.
            walletFileModel.WalletNames.Count().Should().BeGreaterThan(0);
            walletFileModel.WalletNames.Should().Contain(
                Path.GetFileNameWithoutExtension(this.fixture.WalletWithFundsFilePath).Replace(".wallet",""));
        }

        [Fact]
        public async Task SignMessageAsync()
        {
            // Act.
            string signatureResult = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/signmessage")
                .PostJsonAsync(new SignMessageRequest
                {
                    WalletName = this.fixture.walletWithFundsName,
                    ExternalAddress = this.fixture.addressWithFunds,
                    Password = this.fixture.walletWithFundsPassword,
                    Message = this.fixture.signatureMessage
                })
                .ReceiveJson<string>();

            // Assert.
            signatureResult.Should().Be(this.fixture.validSignature, $"Signature is invalid.");
            Encoders.Base64.DecodeData(signatureResult).Should().BeOfType<byte[]>($"Signature was not a {typeof(byte[])} type.");
        }

        [Fact]
        public async Task VerifyValidSignatureAsync()
        {
            // Act.
            bool verifyMessageResult = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/verifymessage")
                .PostJsonAsync(new VerifyRequest
                {
                    Signature = this.fixture.validSignature,
                    ExternalAddress = this.fixture.addressWithFunds,
                    Message = this.fixture.signatureMessage
                })
                .ReceiveJson<bool>();

            // Assert.
            verifyMessageResult.Should().Be(true, "Invalid signature detected.");
        }

        [Fact]
        public async Task VerifyInvalidSignatureAsync()
        {
            // Act.
            bool verifyMessageResult = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/verifymessage")
                .PostJsonAsync(new VerifyRequest
                {
                    Signature = "invalid signature",
                    ExternalAddress = this.fixture.addressWithFunds,
                    Message = this.fixture.signatureMessage
                })
                .ReceiveJson<bool>();

            // Assert.
            verifyMessageResult.Should().Be(false, "Signature Verification failed");
        }

        [Fact]
        public async Task VerifyMessageWithInvalidAddressAsync()
        {
            // Act.
            bool verifyMessageResult = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/verifymessage")
                .PostJsonAsync(new VerifyRequest
                {
                    Signature = this.fixture.validSignature,
                    ExternalAddress = this.fixture.addressWithoutFunds,
                    Message = this.fixture.signatureMessage
                })
                .ReceiveJson<bool>();

            // Assert.
            verifyMessageResult.Should().Be(false, "Signature Verification failed");
        }

        [Fact]
        public async Task VerifyMessageWithInvalidMessageAsync()
        {
            // Act.
            bool verifyMessageResult = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/verifymessage")
                .PostJsonAsync(new VerifyRequest
                {
                    Signature = this.fixture.validSignature,
                    ExternalAddress = this.fixture.addressWithFunds,
                    Message = "Test test..."
                })
                .ReceiveJson<bool>();

            // Assert.
            verifyMessageResult.Should().Be(false, "Signature Verification failed");
        }

        [Fact]
        public async Task VerifyMessageWithAllInvalidAsync()
        {
            // Act.
            bool verifyMessageResult = await $"http://localhost:{this.fixture.Node.ApiPort}/api"
                .AppendPathSegment("wallet/verifymessage")
                .PostJsonAsync(new VerifyRequest
                {
                    Signature = "invalid signature",
                    ExternalAddress = this.fixture.addressWithoutFunds,
                    Message = "Test test..."
                })
                .ReceiveJson<bool>();

            // Assert.
            verifyMessageResult.Should().Be(false, "Signature Verification failed");
        }
    }
}
