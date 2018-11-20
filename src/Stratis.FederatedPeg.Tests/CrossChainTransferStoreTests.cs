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
        private const string walletPassword = "123";
        private Network network;
        private ConcurrentChain chain;
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
        private ExtKey[] federationKeys;
        private ExtKey extendedKey;
        private Script redeemScript
        {
            get
            {
                return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, this.federationKeys.Select(k => k.PrivateKey.PubKey).ToArray());
            }
        }

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
            this.chain = new ConcurrentChain(this.network);

            this.federationGatewaySettings.MinCoinMaturity.Returns(1);
            this.federationGatewaySettings.TransactionFee.Returns(new Money(0.01m, MoneyUnit.BTC));

            // Generate the keys used by the federation members for our tests.
            this.federationKeys = new[]
            {
                "air transfer hello zebra into trick riot elevator maze boring escape wine",
                "steel evil vivid settle render tobacco trumpet bundle track reveal olympic ski",
                "public human shoe cram flee deer claw arch equal ghost betray canal",
                "world joy bundle business wealth price timber salt tilt mesh achieve inmate",
                "dawn best alone urban visa fine mouse dwarf divorce mercy crawl slab"
            }.Select(m => HdOperations.GetExtendedKey(m)).ToArray();

            SetExtendedKey(0);

            this.blockDict = new Dictionary<uint256, Block>();
            this.blockDict[this.network.GenesisHash] = this.network.GetGenesis();

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

        /// <summary>
        /// Chooses the key we use.
        /// </summary>
        /// <param name="keyNum">The key number.</param>
        private void SetExtendedKey(int keyNum)
        {
            this.extendedKey = this.federationKeys[keyNum];

            this.federationGatewaySettings.IsMainChain.Returns(false);
            this.federationGatewaySettings.MultiSigRedeemScript.Returns(this.redeemScript);
            this.federationGatewaySettings.MultiSigAddress.Returns(this.redeemScript.Hash.GetAddress(this.network));
            this.federationGatewaySettings.PublicKey.Returns(this.extendedKey.PrivateKey.PubKey.ToHex());
            this.withdrawalExtractor = new WithdrawalExtractor(this.loggerFactory, this.federationGatewaySettings, this.opReturnDataReader, this.network);
        }

        private void AddFunding()
        {
            Transaction tran1 = this.network.CreateTransaction();
            Transaction tran2 = this.network.CreateTransaction();

            tran1.Outputs.Add(new TxOut(Money.COIN * 90, this.wallet.MultiSigAddress.ScriptPubKey));
            tran1.Outputs.Add(new TxOut(Money.COIN * 80, this.wallet.MultiSigAddress.ScriptPubKey));
            tran2.Outputs.Add(new TxOut(Money.COIN * 70, this.wallet.MultiSigAddress.ScriptPubKey));

            tran1.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tran2.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));

            this.fundingTransactions = new[] { tran1, tran2 };

            this.AppendBlock(tran1);
            this.AppendBlock(tran2);
        }

        /// <summary>
        /// Create the wallet manager and wallet transaction handler.
        /// </summary>
        /// <param name="dataFolder">The data folder.</param>
        private void CreateWalletManagerAndTransactionHandler(DataFolder dataFolder)
        {
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
                this.federationGatewaySettings);

            // Starts and creates the wallet.
            this.federationWalletManager.Start();
            this.wallet = this.federationWalletManager.GetWallet();
            this.federationWalletTransactionHandler = new FederationWalletTransactionHandler(this.loggerFactory, this.federationWalletManager, this.walletFeePolicy, this.network);

            var storeSettings = (StoreSettings)FormatterServices.GetUninitializedObject(typeof(StoreSettings));

            this.federationWalletSyncManager = new FederationWalletSyncManager(this.loggerFactory, this.federationWalletManager, this.chain, this.network,
                this.blockRepository, storeSettings, Substitute.For<INodeLifetime>());

            this.federationWalletSyncManager.Start();

            // Set up the encrypted seed on the wallet.
            string encryptedSeed = this.extendedKey.PrivateKey.GetEncryptedBitcoinSecret(walletPassword, this.network).ToWif();
            this.wallet.EncryptedSeed = encryptedSeed;

            this.federationWalletManager.Secret = new WalletSecret() { WalletPassword = walletPassword };
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithWallet()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(dataFolder);
            this.AppendBlocks(5);

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, this.chain, this.federationGatewaySettings, this.dateTimeProvider,
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
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(dataFolder);
            this.AppendBlocks(5);

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, this.chain, this.federationGatewaySettings, this.dateTimeProvider,
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

            // Force a reorg by creating a new chain that only has genesis in common.
            newTest.CreateWalletManagerAndTransactionHandler(dataFolder);
            newTest.AppendBlocks(3);

            using (var crossChainTransferStore2 = new CrossChainTransferStore(newTest.network, dataFolder, newTest.chain, newTest.federationGatewaySettings, newTest.dateTimeProvider,
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
        /// Recording deposits when the wallet UTXOs are sufficient succeeds with deterministic transactions.
        /// </summary>
        [Fact]
        public void StoringDepositsWhenWalletBalanceSufficientSucceedsWithDeterministicTransactions()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(dataFolder);
            this.AddFunding();
            this.AppendBlocks(5);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, this.chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                Deposit deposit2 = new Deposit(1, new Money(60m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                crossChainTransferStore.RecordLatestMatureDepositsAsync(new[] { deposit1, deposit2 }).GetAwaiter().GetResult();

                Transaction[] transactions = crossChainTransferStore.GetTransactionsByStatusAsync(CrossChainTransferStatus.Partial).GetAwaiter().GetResult().Values.ToArray();

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
                Assert.Single(transactions[1].Inputs);
                Assert.Equal(this.fundingTransactions[1].GetHash(), transactions[1].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[1].Inputs[0].PrevOut.N);

                // Transaction[1] outputs.
                Assert.Equal(3, transactions[1].Outputs.Count);

                // Transaction[1] output value - change.
                Assert.Equal(new Money(9.99m, MoneyUnit.BTC), transactions[1].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[1].Outputs[0].ScriptPubKey);

                // Transaction[1] output value - recipient 2.
                Assert.Equal(new Money(60m, MoneyUnit.BTC), transactions[1].Outputs[1].Value);
                Assert.Equal(address2.ScriptPubKey, transactions[1].Outputs[1].ScriptPubKey);

                // Transaction[1] output value - op_return.
                Assert.Equal(new Money(0m, MoneyUnit.BTC), transactions[1].Outputs[2].Value);
                Assert.Equal(deposit2.Id.ToString(), new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[1]));

                ICrossChainTransfer[] transfers = crossChainTransferStore.GetAsync(new uint256[] { 0, 1 }).GetAwaiter().GetResult().ToArray();

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[0].Status);
                Assert.Equal(deposit1.Amount, new Money(transfers[0].DepositAmount));
                Assert.Equal(address1.ScriptPubKey, transfers[0].DepositTargetAddress);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[1].Status);
                Assert.Equal(deposit2.Amount, new Money(transfers[1].DepositAmount));
                Assert.Equal(address2.ScriptPubKey, transfers[1].DepositTargetAddress);
            }
        }

        /// <summary>
        /// Tests whether the store merges signatures as expected.
        /// </summary>
        [Fact]
        public void StoreMergesSignaturesAsExpected()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(dataFolder);
            this.AddFunding();
            this.AppendBlocks(5);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, this.chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit = new Deposit(0, new Money(160m, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                crossChainTransferStore.RecordLatestMatureDepositsAsync(new[] { deposit }).GetAwaiter().GetResult();

                ICrossChainTransfer crossChainTransfer = crossChainTransferStore.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                Transaction transaction = crossChainTransfer.PartialTransaction;

                Assert.True(CrossChainTransferStore.ValidateTransaction(transaction, this.wallet));

                // Create a separate instance to generate another transaction.
                Transaction transaction2;
                var newTest = new CrossChainTransferStoreTests();
                DataFolder dataFolder2 = new DataFolder(CreateTestDir(this));

                newTest.federationKeys = this.federationKeys;
                newTest.SetExtendedKey(1);
                newTest.CreateWalletManagerAndTransactionHandler(dataFolder2);
                newTest.AddFunding();
                newTest.AppendBlocks(3);

                using (var crossChainTransferStore2 = new CrossChainTransferStore(newTest.network, dataFolder2, newTest.chain, newTest.federationGatewaySettings, newTest.dateTimeProvider,
                    newTest.loggerFactory, newTest.withdrawalExtractor, newTest.fullNode, newTest.blockRepository, newTest.federationWalletManager, newTest.federationWalletTransactionHandler))
                {
                    crossChainTransferStore2.Initialize();
                    crossChainTransferStore2.Start();

                    Assert.Equal(newTest.chain.Tip.HashBlock, crossChainTransferStore2.TipHashAndHeight.Hash);
                    Assert.Equal(newTest.chain.Tip.Height, crossChainTransferStore2.TipHashAndHeight.Height);

                    crossChainTransferStore2.RecordLatestMatureDepositsAsync(new[] { deposit }).GetAwaiter().GetResult();

                    ICrossChainTransfer crossChainTransfer2 = crossChainTransferStore2.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                    Assert.NotNull(crossChainTransfer2);

                    transaction2 = crossChainTransfer2.PartialTransaction;

                    Assert.True(CrossChainTransferStore.ValidateTransaction(transaction2, newTest.wallet));
                }

                // Merges the transaction signatures.
                crossChainTransferStore.MergeTransactionSignaturesAsync(deposit.Id, new[] { transaction2 }).GetAwaiter().GetResult();

                // Test the outcome.
                crossChainTransfer = crossChainTransferStore.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);
                Assert.Equal(CrossChainTransferStatus.FullySigned, crossChainTransfer.Status);

                // Should be returned as signed.
                Transaction signedTransaction = crossChainTransferStore.GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).GetAwaiter().GetResult().Values.SingleOrDefault();
                Assert.NotNull(signedTransaction);

                // Check ths signature.
                Assert.True(CrossChainTransferStore.ValidateTransaction(signedTransaction, this.wallet, true));
            }
        }

        /// <summary>
        /// Tests that the store can recover from the chain and handle a re-org.
        /// </summary>
        [Fact]
        public void StoreRecoversFromChain()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(dataFolder);
            this.AddFunding();
            this.AppendBlocks(3);

            Transaction transaction = this.network.CreateTransaction("0100000002d2eefdc21fb0598664f8fcc8e7866969b9d7bb53f9f8dfb7823ee3eeadb33cbf00000000fd410100473044022038e16c1d58982bf964c186e4d72d372d16a3a63883fa7608261cdb0fdef519e50220149791d4956f0d6ddd61f754274513b2d5ad5fc1df1bed02b1f0f1c7df5322cd01483045022100eb553d5f199cd0aad40b7dd5773741f136e23cf681cc2fb7c2d66a07c2110c3d02207ccbe4547b92ba0ea2e1a01811325ee406c11e5bc832df9c40df9e0bfac59d0a014cad52210332195438ff52eb495321ffb598054e95c8b9aced0238ecf389a55e87f9d26c732103b1af6922b8fc1cc05bec47fdb54f2f85d00a3b4152c7d4bdb5a1e62fea558a362102c426f49fbf65601d7e59bf39cb69a1d488c098aa36caf99cd6ec236830279cdc2102b6128cc9fd484cb2024693e2afbcabecabf44faaa10306459c51a70bb8f510f62103b40dd94129f50521d38b7014295a5c48f2be01eceef2336cc44f321cc613a0c655aeffffffffd2eefdc21fb0598664f8fcc8e7866969b9d7bb53f9f8dfb7823ee3eeadb33cbf01000000fd420100483045022100e232b626ca289b6fe76b4ed011430d67a0243f5a135d5342ea3cd6b02f6bdb780220679de7791bba313373ea906d4236b48358fb00934ac658ad889a7f0e5654c4a301483045022100b698390a1d0c67495be0e14f56e75520e39fa6b01e60e82cc19be32b63b8cc0602206f146ce0324ab795faf19e0802d03c8f036c5cf7ce25d8a70b9b3f17438c2fbf014cad52210332195438ff52eb495321ffb598054e95c8b9aced0238ecf389a55e87f9d26c732103b1af6922b8fc1cc05bec47fdb54f2f85d00a3b4152c7d4bdb5a1e62fea558a362102c426f49fbf65601d7e59bf39cb69a1d488c098aa36caf99cd6ec236830279cdc2102b6128cc9fd484cb2024693e2afbcabecabf44faaa10306459c51a70bb8f510f62103b40dd94129f50521d38b7014295a5c48f2be01eceef2336cc44f321cc613a0c655aeffffffff03c0878b3b0000000017a914623b4eab628d77b96c41df080122ac2c037954138700a0acb9030000001976a914b79f48d2e34702cf134bfbbd3f7787c1db4acd3688ac0000000000000000226a20000000000000000000000000000000000000000000000000000000000000000000000000");
            this.AppendBlock(transaction);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, this.chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.withdrawalExtractor, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                ICrossChainTransfer transfer = crossChainTransferStore.GetAsync(new uint256[] { 0 }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(transfer);
                Assert.Equal(transaction.GetHash(), transfer.PartialTransaction.GetHash());
                Assert.Equal(CrossChainTransferStatus.SeenInBlock, transfer.Status);

                // Re-org the chain.
                this.chain.SetTip(this.chain.Tip.Previous);
                this.federationWalletManager.UpdateLastBlockSyncedHeight(this.chain.Tip);

                transfer = crossChainTransferStore.GetAsync(new uint256[] { 0 }).GetAwaiter().GetResult().SingleOrDefault();

                // Check that the status reverts for a transaction that is no longer visible on the chain.
                Assert.Equal(CrossChainTransferStatus.FullySigned, transfer.Status);

                // Restore the chain.
                AppendBlock(transaction);
                this.federationWalletManager.UpdateLastBlockSyncedHeight(this.chain.Tip);

                transfer = crossChainTransferStore.GetAsync(new uint256[] { 0 }).GetAwaiter().GetResult().SingleOrDefault();

                // Check that the status reverts for a transaction that is again visible on the chain.
                Assert.Equal(CrossChainTransferStatus.SeenInBlock, transfer.Status);
            }
        }

        /// <summary>
        /// Builds a chain with the requested number of blocks.
        /// </summary>
        /// <param name="blocks">The number of blocks.</param>
        private void AppendBlocks(int blocks)
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
        private ChainedHeader AppendBlock(params Transaction[] transactions)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();

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
            block.Header.Nonce = nonce;
            if (!this.chain.TrySetTip(block.Header, out last))
                throw new InvalidOperationException("Previous not existing");
            this.blockDict[block.GetHash()] = block;

            this.federationWalletSyncManager.ProcessBlock(block);

            return last;
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
