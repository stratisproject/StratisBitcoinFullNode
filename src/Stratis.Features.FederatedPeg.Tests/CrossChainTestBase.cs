using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Networks;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CrossChainTestBase
    {
        protected const string walletPassword = "password";
        protected Network network;
        protected ConcurrentChain chain;
        protected ILoggerFactory loggerFactory;
        protected ILogger logger;
        protected IDateTimeProvider dateTimeProvider;
        protected IOpReturnDataReader opReturnDataReader;
        protected IWithdrawalExtractor withdrawalExtractor;
        protected IBlockRepository blockRepository;
        protected IFullNode fullNode;
        protected IFederationWalletManager federationWalletManager;
        protected IFederationGatewaySettings federationGatewaySettings;
        protected IFederationWalletSyncManager federationWalletSyncManager;
        protected IFederationWalletTransactionHandler federationWalletTransactionHandler;
        protected IWithdrawalTransactionBuilder withdrawalTransactionBuilder;
        protected DataFolder dataFolder;
        protected IWalletFeePolicy walletFeePolicy;
        protected IAsyncLoopFactory asyncLoopFactory;
        protected INodeLifetime nodeLifetime;
        protected IConnectionManager connectionManager;
        protected DBreezeSerializer dBreezeSerializer;
        protected Dictionary<uint256, Block> blockDict;
        protected List<Transaction> fundingTransactions;
        protected FederationWallet wallet;
        protected ExtKey[] federationKeys;
        protected ExtKey extendedKey;
        protected Script redeemScript
        {
            get
            {
                return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, this.federationKeys.Select(k => k.PrivateKey.PubKey).ToArray());
            }
        }

        /// <summary>
        /// Initializes the cross-chain transfer tests.
        /// </summary>
        /// <param name="network">The network to run the tests for.</param>
        public CrossChainTestBase(Network network = null)
        {
            this.network = network ?? FederatedPegNetwork.NetworksSelector.Regtest();
            NetworkRegistration.Register(this.network);

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.asyncLoopFactory = new AsyncLoopFactory(this.loggerFactory);
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.dateTimeProvider = DateTimeProvider.Default;
            this.opReturnDataReader = new OpReturnDataReader(this.loggerFactory, this.network);
            this.blockRepository = Substitute.For<IBlockRepository>();
            this.fullNode = Substitute.For<IFullNode>();
            this.withdrawalTransactionBuilder = Substitute.For<IWithdrawalTransactionBuilder>();
            this.federationWalletManager = Substitute.For<IFederationWalletManager>();
            this.federationWalletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.federationWalletTransactionHandler = Substitute.For<IFederationWalletTransactionHandler>();
            this.walletFeePolicy = Substitute.For<IWalletFeePolicy>();
            this.nodeLifetime = new NodeLifetime();
            this.connectionManager = Substitute.For<IConnectionManager>();
            this.dBreezeSerializer = new DBreezeSerializer(this.network);

            this.wallet = null;
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            this.chain = new ConcurrentChain(this.network);

            this.federationGatewaySettings.MinCoinMaturity.Returns(1);
            this.federationGatewaySettings.TransactionFee.Returns(new Money(0.01m, MoneyUnit.BTC));

            // Generate the keys used by the federation members for our tests.
            this.federationKeys = new[]
            {
                "ensure feel swift crucial bridge charge cloud tell hobby twenty people mandate",
                "quiz sunset vote alley draw turkey hill scrap lumber game differ fiction",
                "exchange rent bronze pole post hurry oppose drama eternal voice client state"
            }.Select(m => HdOperations.GetExtendedKey(m)).ToArray();

            SetExtendedKey(0);

            this.fundingTransactions = new List<Transaction>();

            this.blockDict = new Dictionary<uint256, Block>();
            this.blockDict[this.network.GenesisHash] = this.network.GetGenesis();

            this.blockRepository.GetBlocksAsync(Arg.Any<List<uint256>>()).ReturnsForAnyArgs((x) => {
                var hashes = x.ArgAt<List<uint256>>(0);
                var blocks = new List<Block>();
                for (int i = 0; i < hashes.Count; i++)
                {
                    blocks.Add(this.blockDict.TryGetValue(hashes[i], out Block block) ? block : null);
                }

                return blocks;
            });
        }

        /// <summary>
        /// Chooses the key we use.
        /// </summary>
        /// <param name="keyNum">The key number.</param>
        protected void SetExtendedKey(int keyNum)
        {
            this.extendedKey = this.federationKeys[keyNum];

            this.federationGatewaySettings.IsMainChain.Returns(false);
            this.federationGatewaySettings.MultiSigRedeemScript.Returns(this.redeemScript);
            this.federationGatewaySettings.MultiSigAddress.Returns(this.redeemScript.Hash.GetAddress(this.network));
            this.federationGatewaySettings.PublicKey.Returns(this.extendedKey.PrivateKey.PubKey.ToHex());
            this.withdrawalExtractor = new WithdrawalExtractor(this.loggerFactory, this.federationGatewaySettings, this.opReturnDataReader, this.network);
        }

        protected Transaction AddFundingTransaction(Money[] amounts)
        {
            Transaction transaction = this.network.CreateTransaction();

            foreach (Money amount in amounts)
            {
                transaction.Outputs.Add(new TxOut(amount, this.wallet.MultiSigAddress.ScriptPubKey));
            }

            transaction.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));

            this.AppendBlock(transaction);
            this.fundingTransactions.Add(transaction);

            return transaction;
        }

        protected void AddFunding()
        {
            AddFundingTransaction(new Money[] { Money.COIN * 90, Money.COIN * 80 });
            AddFundingTransaction(new Money[] { Money.COIN * 70 });
        }

        /// <summary>
        /// Create the wallet manager and wallet transaction handler.
        /// </summary>
        /// <param name="dataFolder">The data folder.</param>
        protected void Init(DataFolder dataFolder)
        {
            this.dataFolder = dataFolder;

            // Create the wallet manager.
            this.federationWalletManager = new FederationWalletManager(
                this.loggerFactory,
                this.network,
                this.chain,
                dataFolder,
                this.walletFeePolicy,
                this.asyncLoopFactory,
                new NodeLifetime(),
                this.dateTimeProvider,
                this.federationGatewaySettings,
                this.withdrawalExtractor);

            // Starts and creates the wallet.
            this.federationWalletManager.Start();
            this.wallet = this.federationWalletManager.GetWallet();

            // TODO: The transaction builder, cross-chain store and fed wallet tx handler should be tested individually.
            this.federationWalletTransactionHandler = new FederationWalletTransactionHandler(this.loggerFactory, this.federationWalletManager, this.walletFeePolicy, this.network);
            this.withdrawalTransactionBuilder = new WithdrawalTransactionBuilder(this.loggerFactory, this.network, this.federationWalletManager, this.federationWalletTransactionHandler, this.federationGatewaySettings);

            var storeSettings = (StoreSettings)FormatterServices.GetUninitializedObject(typeof(StoreSettings));

            this.federationWalletSyncManager = new FederationWalletSyncManager(this.loggerFactory, this.federationWalletManager, this.chain, this.network,
                this.blockRepository, storeSettings, Substitute.For<INodeLifetime>());

            this.federationWalletSyncManager.Start();

            // Set up the encrypted seed on the wallet.
            string encryptedSeed = this.extendedKey.PrivateKey.GetEncryptedBitcoinSecret(walletPassword, this.network).ToWif();
            this.wallet.EncryptedSeed = encryptedSeed;

            this.federationWalletManager.Secret = new WalletSecret() { WalletPassword = walletPassword };

            System.Reflection.FieldInfo prop = this.federationWalletManager.GetType().GetField("isFederationActive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            prop.SetValue(this.federationWalletManager, true);
        }

        protected ICrossChainTransferStore CreateStore()
        {
            return new CrossChainTransferStore(this.network, this.dataFolder, this.chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.withdrawalTransactionBuilder, this.dBreezeSerializer);
        }

        /// <summary>
        /// Builds a chain with the requested number of blocks.
        /// </summary>
        /// <param name="blocks">The number of blocks.</param>
        protected void AppendBlocks(int blocks)
        {
            for (int i = 0; i < blocks; i++)
            {
                this.AppendBlock();
            }
        }

        /// <summary>
        /// Create a block and add it to the dictionary used by the mock block repository.
        /// </summary>
        /// <param name="transactions">Additional transactions to add to the block.</param>
        /// <returns>The last chained header.</returns>
        protected ChainedHeader AppendBlock(params Transaction[] transactions)
        {
            Block block = this.network.CreateBlock();

            // Create coinbase.
            block.AddTransaction(this.network.CreateTransaction());

            // Add additional transactions if any.
            foreach (Transaction transaction in transactions)
            {
                block.AddTransaction(transaction);
            }

            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = this.chain.Tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();

            return AppendBlock(block);
        }

        /// <summary>
        /// Adds a previously created block to the dictionary used by the mock block repository.
        /// </summary>
        /// <param name="block">The block to add.</param>
        /// <returns>The last chained header.</returns>
        protected ChainedHeader AppendBlock(Block block)
        {
            if (!this.chain.TrySetTip(block.Header, out ChainedHeader last))
                throw new InvalidOperationException("Previous not existing");

            this.blockDict[block.GetHash()] = block;

            this.federationWalletSyncManager.ProcessBlock(block);

            return last;
        }

        /// <summary>
        /// Waits for a function to return true.
        /// </summary>
        /// <param name="act">The function returning <c>true</c> or <c>false</c>.</param>
        /// <param name="failureReason">The failure reason if any.</param>
        /// <param name="retryDelayInMiliseconds">How often to retry in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private static void WaitLoop(Func<bool> act, string failureReason = "Unknown Reason", int retryDelayInMiliseconds = 1000, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken = cancellationToken == default(CancellationToken)
                ? new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 60 * 1000).Token
                : cancellationToken;

            while (!act())
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(retryDelayInMiliseconds);
                }
                catch (OperationCanceledException e)
                {
                    Assert.False(true, $"{failureReason}{Environment.NewLine}{e.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a directory for a test, based on the name of the class containing the test and the name of the test.
        /// </summary>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory that was created.</returns>
        public static string CreateTestDir(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            string directoryPath = GetTestDirectoryPath(caller, callingMethod);
            return AssureEmptyDir(directoryPath);
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> or <see cref="CreateDataFolder(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            return GetTestDirectoryPath(Path.Combine(caller.GetType().Name, callingMethod));
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="testDirectory">The directory in which the test files are contained.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(string testDirectory)
        {
            return Path.Combine("..", "..", "..", "..", "TestCase", testDirectory);
        }

        /// <summary>
        /// Creates a new folder that will be empty.
        /// </summary>
        /// <param name="dir">The first part of the folder name.</param>
        /// <returns>A folder name with the current time concatenated.</returns>
        public static string AssureEmptyDir(string dir)
        {
            string uniqueDirName = $"{dir}-{DateTime.UtcNow:ddMMyyyyTHH.mm.ss.fff}";
            Directory.CreateDirectory(uniqueDirName);
            return uniqueDirName;
        }
    }
}
