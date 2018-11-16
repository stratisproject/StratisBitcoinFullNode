using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Networks;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using Stratis.Sidechains.Networks;
using Xunit;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.FederatedPeg.Tests
{
    public class CrossChainTransferStoreTests
    {
        private Network network;
        private ILoggerFactory loggerFactory;
        private ILogger logger;
        private IDateTimeProvider dateTimeProvider;
        private IOpReturnDataReader opReturnDataReader;
        private IWithdrawalExtractor withdrawalExtractor;
        private IBlockRepository blockRepository;
        private IFullNode fullNode;
        private IFederationWalletManager federationWalletManager;
        private IFederationWalletTransactionHandler federationWalletTransactionHandler;
        private IFederationGatewaySettings federationGatewaySettings;
        private IFederationWalletSyncManager federationWalletSyncManager;
        private IWalletFeePolicy walletFeePolicy;
        private IAsyncLoopFactory asyncLoopFactory;
        private Dictionary<uint256, Block> blockDict;
        private Transaction[] fundingTransactions;
        private FederationWallet wallet;

        /// <summary>
        /// Initializes the cross-chain transfer tests.
        /// </summary>
        public CrossChainTransferStoreTests()
        {
            this.network = ApexNetwork.RegTest;
            NetworkRegistration.Register(this.network);

            DBreezeSerializer serializer = new DBreezeSerializer();
            serializer.Initialize(this.network);

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.asyncLoopFactory = Substitute.For<IAsyncLoopFactory>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.dateTimeProvider = DateTimeProvider.Default;
            this.opReturnDataReader = new OpReturnDataReader(this.loggerFactory, this.network);
            this.blockRepository = Substitute.For<IBlockRepository>();
            this.fullNode = Substitute.For<IFullNode>();
            this.federationWalletManager = Substitute.For<IFederationWalletManager>();
            this.federationWalletTransactionHandler = Substitute.For<IFederationWalletTransactionHandler>();
            this.federationWalletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.walletFeePolicy = Substitute.For<IWalletFeePolicy>();
            this.wallet = null;
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            var redeemScript = new Script("2 026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c 02a97b7d0fad7ea10f456311dcd496ae9293952d4c5f2ebdfc32624195fde14687 02e9d3cd0c2fa501957149ff9d21150f3901e6ece0e3fe3007f2372720c84e3ee1 03c99f997ed71c7f92cf532175cea933f2f11bf08f1521d25eb3cc9b8729af8bf4 034b191e3b3107b71d1373e840c5bf23098b55a355ca959b968993f5dec699fc38 5 OP_CHECKMULTISIG");
            this.federationGatewaySettings.IsMainChain.Returns(false);
            this.federationGatewaySettings.MultiSigRedeemScript.Returns(redeemScript);
            this.federationGatewaySettings.MultiSigAddress.Returns(redeemScript.Hash.GetAddress(this.network));
            this.federationGatewaySettings.PublicKey.Returns("026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c");
            this.withdrawalExtractor = new WithdrawalExtractor(this.loggerFactory, this.federationGatewaySettings, this.opReturnDataReader, Substitute.For<IWithdrawalReceiver>(), this.network);

            this.blockDict = new Dictionary<uint256, Block>();

            this.blockRepository.GetBlocksAsync(Arg.Any<List<uint256>>()).ReturnsForAnyArgs((x) => {
                List<uint256> hashes = x.ArgAt<List<uint256>>(0);
                var blocks = new List<Block>();
                for (int i = 0; i < hashes.Count; i++)
                {
                    blocks.Add(this.blockDict.TryGetValue(hashes[i], out Block block) ? block : null);
                }

                return blocks;
            });
        }

        private void CreateWalletManagerAndTransactionHandler(ConcurrentChain chain, DataFolder dataFolder)
        {
            // Create the wallet manager.
            this.federationWalletManager = new FederationWalletManager(
                this.loggerFactory,
                this.network,
                chain,
                dataFolder,
                this.walletFeePolicy,
                this.asyncLoopFactory,
                new NodeLifetime(),
                this.dateTimeProvider,
                this.federationGatewaySettings);

            // Starts and creates the wallet.
            this.federationWalletManager.Start();
            this.wallet = this.federationWalletManager.GetWallet();

            if (this.wallet.MultiSigAddress.Transactions.Count == 0)
            {
                Transaction tran1 = this.network.CreateTransaction();
                Transaction tran2 = this.network.CreateTransaction();

                tran1.Outputs.Add(new TxOut(Money.COIN * 90, this.wallet.MultiSigAddress.ScriptPubKey));
                tran1.Outputs.Add(new TxOut(Money.COIN * 80, this.wallet.MultiSigAddress.ScriptPubKey));
                tran2.Outputs.Add(new TxOut(Money.COIN * 70, this.wallet.MultiSigAddress.ScriptPubKey));

                tran1.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
                tran2.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));

                this.wallet.MultiSigAddress.Transactions.Add(new Features.FederationGateway.Wallet.TransactionData()
                {
                    Amount = tran1.Outputs[0].Value,
                    Id = tran1.GetHash(),
                    Hex = tran1.ToString(this.network, RawFormat.BlockExplorer),
                    Index = 0,
                    ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                    BlockHeight = 2
                });

                this.wallet.MultiSigAddress.Transactions.Add(new Features.FederationGateway.Wallet.TransactionData()
                {
                    Amount = tran1.Outputs[1].Value,
                    Id = tran1.GetHash(),
                    Hex = tran1.ToString(this.network, RawFormat.BlockExplorer),
                    Index = 1,
                    ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                    BlockHeight = 2
                });

                this.wallet.MultiSigAddress.Transactions.Add(new Features.FederationGateway.Wallet.TransactionData()
                {
                    Amount = tran2.Outputs[0].Value,
                    Id = tran2.GetHash(),
                    Hex = tran2.ToString(this.network, RawFormat.BlockExplorer),
                    Index = 0,
                    ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                    BlockHeight = 3
                });

                this.fundingTransactions = new[] { tran1, tran2 };
            }

            (this.federationWalletManager as FederationWalletManager).LoadKeysLookupLock();

            this.federationWalletTransactionHandler = new FederationWalletTransactionHandler(this.loggerFactory, this.federationWalletManager, this.walletFeePolicy, this.network);

            var storeSettings = (StoreSettings)FormatterServices.GetUninitializedObject(typeof(StoreSettings));

            this.federationWalletSyncManager = new FederationWalletSyncManager(this.loggerFactory, this.federationWalletManager, chain, this.network,
                this.blockRepository, storeSettings, Substitute.For<INodeLifetime>());

            this.federationWalletSyncManager.Start();

            List<Block> blocks = this.blockRepository.GetBlocksAsync(chain.EnumerateAfter(this.network.GenesisHash).Select(h => h.HashBlock).ToList()).GetAwaiter().GetResult();

            foreach (Block block in blocks)
            {
                this.federationWalletSyncManager.ProcessBlock(block);
            }
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithWallet()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(chain, dataFolder);

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(this.wallet.LastBlockSyncedHeight, crossChainTransferStore.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithWalletAndSurvivesRestart()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(chain, dataFolder);

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                this.federationWalletManager.SaveWallet();

                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(this.wallet.LastBlockSyncedHeight, crossChainTransferStore.TipHashAndHeight.Height);
            }

            // Create a new instance of this test that loads from the persistence that we created in the step before.
            var newTest = new CrossChainTransferStoreTests();

            // Force a form by creating a new chain that only has genesis in common.
            ConcurrentChain newChain = newTest.BuildChain(3);
            newTest.CreateWalletManagerAndTransactionHandler(newChain, dataFolder);

            using (var crossChainTransferStore2 = new CrossChainTransferStore(newTest.network, dataFolder, newChain, newTest.federationGatewaySettings, newTest.dateTimeProvider,
                newTest.loggerFactory, newTest.withdrawalExtractor, newTest.fullNode, newTest.blockRepository, newTest.federationWalletManager, newTest.federationWalletTransactionHandler))
            {
                crossChainTransferStore2.Initialize();

                // Test that the store was reloaded from persistence.
                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore2.TipHashAndHeight.Hash);
                Assert.Equal(this.wallet.LastBlockSyncedHeight, crossChainTransferStore2.TipHashAndHeight.Height);

                // Test that synchronizing the store aligns it with the current chain tip after the fork.
                crossChainTransferStore2.Start();

                Assert.Equal(newTest.wallet.LastBlockSyncedHash, crossChainTransferStore2.TipHashAndHeight.Hash);
                Assert.Equal(newTest.wallet.LastBlockSyncedHeight, crossChainTransferStore2.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Recording a deposit creates a <see cref="CrossChainTransferStatus.Rejected" /> transfer if the balance is insufficient.
        /// </summary>
        [Fact]
        public void StoringDepositWhenWalletBalanceSufficientSucceedsWithDeterministicTransaction()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(chain, dataFolder);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                Deposit deposit2 = new Deposit(1, new Money(70m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                crossChainTransferStore.RecordLatestMatureDepositsAsync(new[] { deposit1, deposit2 }).GetAwaiter().GetResult();

                Transaction[] transactions = crossChainTransferStore.GetPartialTransactionsAsync().GetAwaiter().GetResult();

                Assert.Equal(2, transactions.Length);

                // Transactions[0] inputs.
                Assert.Equal(2, transactions[0].Inputs.Count);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[0].Inputs[0].PrevOut.N);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[1].PrevOut.Hash);
                Assert.Equal((uint)1, transactions[0].Inputs[1].PrevOut.N);

                // Transaction[0] outputs.
                Assert.Equal(3, transactions[0].Outputs.Count);

                // Transaction[0] output value - change.
                Assert.Equal(new Money(9.99m, MoneyUnit.BTC), transactions[0].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[0].Outputs[0].ScriptPubKey);

                // Transaction[0] output value - recipient 1.
                Assert.Equal(new Money(160m, MoneyUnit.BTC), transactions[0].Outputs[1].Value);
                Assert.Equal(address1.ScriptPubKey, transactions[0].Outputs[1].ScriptPubKey);

                // Transaction[0] output value - op_return.
                Assert.Equal(new Money(0m, MoneyUnit.BTC), transactions[0].Outputs[2].Value);
                Assert.Equal(deposit1.Id.ToString(), new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[0]));

                // Transactions[1] inputs.
                Assert.Equal(2, transactions[1].Inputs.Count);
                Assert.Equal(transactions[0].GetHash(), transactions[1].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[1].Inputs[0].PrevOut.N);
                Assert.Equal(this.fundingTransactions[1].GetHash(), transactions[1].Inputs[1].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[1].Inputs[1].PrevOut.N);

                // Transaction[1] outputs.
                Assert.Equal(3, transactions[1].Outputs.Count);

                // Transaction[1] output value - change.
                Assert.Equal(new Money(9.98m, MoneyUnit.BTC), transactions[1].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[1].Outputs[0].ScriptPubKey);

                // Transaction[1] output value - recipient 2.
                Assert.Equal(new Money(70m, MoneyUnit.BTC), transactions[1].Outputs[1].Value);
                Assert.Equal(address2.ScriptPubKey, transactions[1].Outputs[1].ScriptPubKey);

                // Transaction[1] output value - op_return.
                Assert.Equal(new Money(0m, MoneyUnit.BTC), transactions[1].Outputs[2].Value);
                Assert.Equal(deposit2.Id.ToString(), new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[1]));

                ICrossChainTransfer[] transfers = crossChainTransferStore.GetAsync(new uint256[] { 0, 1 }).GetAwaiter().GetResult().ToArray();

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[0].Status);
                Assert.Equal(deposit1.Amount, new Money(transfers[0].DepositAmount));
                Assert.Equal(address1.ScriptPubKey, transfers[0].DepositTargetAddress);
                Assert.Equal(crossChainTransferStore.NextMatureDepositHeight - 1, transfers[0].DepositBlockHeight);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[1].Status);
                Assert.Equal(deposit2.Amount, new Money(transfers[1].DepositAmount));
                Assert.Equal(address2.ScriptPubKey, transfers[1].DepositTargetAddress);
                Assert.Equal(crossChainTransferStore.NextMatureDepositHeight - 1, transfers[1].DepositBlockHeight);
            }
        }

        [Fact]
        public void StoreMergesSignaturesAsExpected()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(chain, dataFolder);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit = new Deposit(0, new Money(160m, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                crossChainTransferStore.RecordLatestMatureDepositsAsync(new[] { deposit }).GetAwaiter().GetResult();

                ICrossChainTransfer crossChainTransfer = crossChainTransferStore.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                Transaction transaction = crossChainTransfer.PartialTransaction;

                var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic);
                string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret("123", this.network).ToWif();
                this.wallet.EncryptedSeed = encryptedSeed;

                // TODO
                /*

                TransactionBuilder builder = new TransactionBuilder(this.network);

                Transaction signed = builder
                    .AddKeys(multiSigAddress.GetPrivateKey(this.wallet.EncryptedSeed, "123", this.network))
                    .SignTransactionInPlace(transaction);

                TransactionSignature[] signatures = PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(this.network, signed.Inputs[1].ScriptSig);
                */
            }
        }

        /// <summary>
        /// Builds a chain with the requested number of blocks.
        /// </summary>
        /// <param name="blocks">The number of blocks.</param>
        /// <returns>A chain with the requested number of blocks.</returns>
        private ConcurrentChain BuildChain(int blocks)
        {
            ConcurrentChain chain = new ConcurrentChain(this.network);

            this.blockDict.Clear();
            this.blockDict[this.network.GenesisHash] = this.network.GetGenesis();

            for (int i = 0; i < blocks - 1; i++)
            {
                this.AppendBlock(chain);
            }

            return chain;
        }

        /// <summary>
        /// Create a block and add it to the dictionary used by the mock block repository.
        /// </summary>
        /// <param name="previous">Previous chained header.</param>
        /// <param name="chains">Chains to add the block to.</param>
        /// <returns>The last chained header.</returns>
        private ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = this.network.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
                this.blockDict[block.GetHash()] = block;
            }
            return last;
        }

        /// <summary>
        /// Append a block to the specified chain(s).
        /// </summary>
        /// <param name="chains">The chains to append a block to.</param>
        /// <returns>The last chained header.</returns>
        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chains);
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

        public static string AssureEmptyDir(string dir)
        {
            string uniqueDirName = $"{dir}-{DateTime.UtcNow:ddMMyyyyTHH.mm.ss.fff}";
            Directory.CreateDirectory(uniqueDirName);
            return uniqueDirName;
        }
    }
}
