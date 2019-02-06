using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DBreeze;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Patricia;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Xunit;
using Key = NBitcoin.Key;


namespace Stratis.SmartContracts.IntegrationTests.PoW
{
    /// <summary>
    /// This is taken from 'MinerTests.cs' and adjusted to use a different block validator.
    /// </summary>
    public sealed class SmartContractMinerTests
    {
        private static readonly FeeRate blockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);

        public static BlockDefinition AssemblerForTest(TestContext testContext)
        {
            return new SmartContractBlockDefinition(
                new BlockBufferGenerator(),
                testContext.cachedCoinView,
                testContext.consensusManager,
                DateTimeProvider.Default,
                testContext.ExecutorFactory,
                new LoggerFactory(),
                testContext.mempool,
                testContext.mempoolLock,
                new MinerSettings(testContext.NodeSettings),
                testContext.network,
                new SenderRetriever(),
                testContext.StateRoot);
        }

        public class Blockinfo
        {
            public int extranonce;
            public uint nonce;
        }

        public static long[,] blockinfoarr =
        {
            {4, 0xa4a3e223}, {2, 0x15c32f9e}, {1, 0x0375b547}, {1, 0x7004a8a5},
            {2, 0xce440296}, {2, 0x52cfe198}, {1, 0x77a72cd0}, {2, 0xbb5d6f84},
            {2, 0x83f30c2c}, {1, 0x48a73d5b}, {1, 0xef7dcd01}, {2, 0x6809c6c4},
            {2, 0x0883ab3c}, {1, 0x087bbbe2}, {2, 0x2104a814}, {2, 0xdffb6daa},
            {1, 0xee8a0a08}, {2, 0xba4237c1}, {1, 0xa70349dc}, {1, 0x344722bb},
            {3, 0xd6294733}, {2, 0xec9f5c94}, {2, 0xca2fbc28}, {1, 0x6ba4f406},
            {2, 0x015d4532}, {1, 0x6e119b7c}, {2, 0x43e8f314}, {2, 0x27962f38},
            {2, 0xb571b51b}, {2, 0xb36bee23}, {2, 0xd17924a8}, {2, 0x6bc212d9},
            {1, 0x630d4948}, {2, 0x9a4c4ebb}, {2, 0x554be537}, {1, 0xd63ddfc7},
            {2, 0xa10acc11}, {1, 0x759a8363}, {2, 0xfb73090d}, {1, 0xe82c6a34},
            {1, 0xe33e92d7}, {3, 0x658ef5cb}, {2, 0xba32ff22}, {5, 0x0227a10c},
            {1, 0xa9a70155}, {5, 0xd096d809}, {1, 0x37176174}, {1, 0x830b8d0f},
            {1, 0xc6e3910e}, {2, 0x823f3ca8}, {1, 0x99850849}, {1, 0x7521fb81},
            {1, 0xaacaabab}, {1, 0xd645a2eb}, {5, 0x7aea1781}, {5, 0x9d6e4b78},
            {1, 0x4ce90fd8}, {1, 0xabdc832d}, {6, 0x4a34f32a}, {2, 0xf2524c1c},
            {2, 0x1bbeb08a}, {1, 0xad47f480}, {1, 0x9f026aeb}, {1, 0x15a95049},
            {2, 0xd1cb95b2}, {2, 0xf84bbda5}, {1, 0x0fa62cd1}, {1, 0xe05f9169},
            {1, 0x78d194a9}, {5, 0x3e38147b}, {5, 0x737ba0d4}, {1, 0x63378e10},
            {1, 0x6d5f91cf}, {2, 0x88612eb8}, {2, 0xe9639484}, {1, 0xb7fabc9d},
            {2, 0x19b01592}, {1, 0x5a90dd31}, {2, 0x5bd7e028}, {2, 0x94d00323},
            {1, 0xa9b9c01a}, {1, 0x3a40de61}, {1, 0x56e7eec7}, {5, 0x859f7ef6},
            {1, 0xfd8e5630}, {1, 0x2b0c9f7f}, {1, 0xba700e26}, {1, 0x7170a408},
            {1, 0x70de86a8}, {1, 0x74d64cd5}, {1, 0x49e738a1}, {2, 0x6910b602},
            {0, 0x643c565f}, {1, 0x54264b3f}, {2, 0x97ea6396}, {2, 0x55174459},
            {2, 0x03e8779a}, {1, 0x98f34d8f}, {1, 0xc07b2b07}, {1, 0xdfe29668},
            {1, 0x3141c7c1}, {1, 0xb3b595f4}, {1, 0x735abf08}, {5, 0x623bfbce},
            {2, 0xd351e722}, {1, 0xf4ca48c9}, {1, 0x5b19c670}, {1, 0xa164bf0e},
            {2, 0xbbbeb305}, {2, 0xfe1c810a}
        };

        public bool TestSequenceLocks(TestContext testContext, ChainedHeader chainedBlock, Transaction tx, Transaction.LockTimeFlags flags, LockPoints uselock = null)
        {
            var context = new MempoolValidationContext(tx, new MempoolValidationState(false))
            {
                View = new MempoolCoinView(testContext.cachedCoinView, testContext.mempool, testContext.mempoolLock, null)
            };

            context.View.LoadViewAsync(tx).GetAwaiter().GetResult();

            return MempoolValidator.CheckSequenceLocks(testContext.network, chainedBlock, context, flags, uselock, false);
        }

        public class TestContext
        {
            public List<Blockinfo> blockinfo;
            private ExtendedLoggerFactory loggerFactory;
            internal uint Nonce { get; set; }
            public Network network;
            internal NodeSettings NodeSettings { get; private set; }
            public Script scriptPubKey;
            public BlockTemplate newBlock;
            public Transaction tx, tx2;
            public Script script;
            public uint256 hash;
            public TestMemPoolEntryHelper entry;
            public ConcurrentChain chain;
            public ConsensusManager consensusManager;
            public ConsensusRuleEngine consensusRules;
            public TxMempool mempool;
            public MempoolSchedulerLock mempoolLock;
            public List<Transaction> txFirst;
            public Money BLOCKSUBSIDY = 50 * Money.COIN;
            public Money LOWFEE = Money.CENT;
            public Money HIGHFEE = Money.COIN;
            public Money HIGHERFEE = 4 * Money.COIN;
            public int baseheight;
            public CachedCoinView cachedCoinView;

            #region Smart Contract Components

            internal AddressGenerator AddressGenerator { get; private set; }
            private bool useCheckpoints = true;
            public Key privateKey;
            private ReflectionVirtualMachine vm;
            private ISerializer serializer;
            private ContractAssemblyLoader assemblyLoader;
            public ICallDataSerializer callDataSerializer;
            internal ReflectionExecutorFactory ExecutorFactory { get; private set; }
            internal string Folder { get; private set; }
            private InternalExecutorFactory internalTxExecutorFactory;
            private IKeyEncodingStrategy keyEncodingStrategy;
            private IContractModuleDefinitionReader moduleDefinitionReader;
            private StateFactory stateFactory;
            private IContractPrimitiveSerializer primitiveSerializer;
            internal Key PrivateKey { get; private set; }
            private ReflectionVirtualMachine reflectionVirtualMachine;
            private IContractRefundProcessor refundProcessor;
            internal StateRepositoryRoot StateRoot { get; private set; }
            private IContractTransferProcessor transferProcessor;
            private SmartContractValidator validator;
            private StateProcessor stateProcessor;
            private SmartContractStateFactory smartContractStateFactory;

            #endregion

            public async Task InitializeAsync([CallerMemberName] string callingMethod = "")
            {
                this.blockinfo = new List<Blockinfo>();
                List<long> lst = blockinfoarr.Cast<long>().ToList();
                for (int i = 0; i < lst.Count; i += 2)
                    this.blockinfo.Add(new Blockinfo { extranonce = (int)lst[i], nonce = (uint)lst[i + 1] });

                // Note that by default, these tests run with size accounting enabled.
                this.network = new SmartContractsRegTest();
                this.PrivateKey = new Key();
                this.scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(this.PrivateKey.PubKey);

                this.entry = new TestMemPoolEntryHelper();
                this.chain = new ConcurrentChain(this.network);
                this.network.Consensus.Options = new ConsensusOptions();

                IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;
                var inMemoryCoinView = new InMemoryCoinView(this.chain.Tip.HashBlock);
                this.cachedCoinView = new CachedCoinView(inMemoryCoinView, dateTimeProvider, new LoggerFactory(), new NodeStats(new DateTimeProvider()));

                this.loggerFactory = new ExtendedLoggerFactory();
                this.loggerFactory.AddConsoleWithFilters();

                this.NodeSettings = new NodeSettings(this.network, args: new string[] { "-checkpoints" });
                var consensusSettings = new ConsensusSettings(this.NodeSettings);

                var nodeDeployments = new NodeDeployments(this.network, this.chain);

                var senderRetriever = new SenderRetriever();

                var genesis = this.network.GetGenesis();

                var chainState = new ChainState()
                {
                    BlockStoreTip = new ChainedHeader(genesis.Header, genesis.GetHash(), 0)
                };

                InitializeSmartContractComponents(callingMethod);

                this.consensusRules = new SmartContractPowConsensusRuleEngine(
                    this.callDataSerializer,
                    this.chain,
                    new Checkpoints(),
                    consensusSettings,
                    DateTimeProvider.Default,
                    this.ExecutorFactory,
                    this.loggerFactory,
                    this.network,
                    nodeDeployments,
                    this.StateRoot,
                    new PersistentReceiptRepository(new DataFolder(this.Folder)),
                    senderRetriever,
                    this.cachedCoinView,
                    chainState,
                    new InvalidBlockHashStore(DateTimeProvider.Default),
                    new NodeStats(new DateTimeProvider()))
                    .Register();

                this.consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network, chainState: chainState, inMemoryCoinView: inMemoryCoinView, chain: this.chain, ruleRegistration: new SmartContractPowRuleRegistration(this.network), consensusRules: this.consensusRules);

                await this.consensusManager.InitializeAsync(chainState.BlockStoreTip);

                this.entry.Fee(11);
                this.entry.Height(11);

                var dateTimeProviderSet = new DateTimeProviderSet
                {
                    time = DateTimeProvider.Default.GetTime(),
                    timeutc = DateTimeProvider.Default.GetUtcNow()
                };

                this.mempool = new TxMempool(dateTimeProviderSet, new BlockPolicyEstimator(new MempoolSettings(this.NodeSettings), this.loggerFactory, this.NodeSettings), this.loggerFactory, this.NodeSettings);
                this.mempoolLock = new MempoolSchedulerLock();

                var blocks = new List<NBitcoin.Block>();
                this.txFirst = new List<Transaction>();

                this.Nonce = 0;

                for (int i = 0; i < this.blockinfo.Count; ++i)
                {
                    NBitcoin.Block block = this.network.CreateBlock();
                    block.Header.HashPrevBlock = this.consensusManager.Tip.HashBlock;
                    ((SmartContractBlockHeader)block.Header).HashStateRoot = ((SmartContractBlockHeader)genesis.Header).HashStateRoot;
                    block.Header.Version = 1;
                    block.Header.Time = Utils.DateTimeToUnixTime(this.chain.Tip.GetMedianTimePast()) + 1;

                    Transaction coinbaseTx = this.network.CreateTransaction();
                    coinbaseTx.Version = 1;
                    coinbaseTx.AddInput(new TxIn(new Script(new[] { Op.GetPushOp(this.blockinfo[i].extranonce), Op.GetPushOp(this.chain.Height) })));
                    coinbaseTx.AddOutput(new TxOut(Money.Coins(50), this.scriptPubKey));
                    coinbaseTx.AddOutput(new TxOut(Money.Zero, new Script()));
                    block.AddTransaction(coinbaseTx);

                    if (this.txFirst.Count < 4)
                        this.txFirst.Add(block.Transactions[0]);

                    block.Header.Bits = block.Header.GetWorkRequired(this.network, this.chain.Tip);

                    block.UpdateMerkleRoot();

                    while (!block.CheckProofOfWork())
                        block.Header.Nonce = ++this.Nonce;

                    // Serialization sets the BlockSize property.
                    block = NBitcoin.Block.Load(block.ToBytes(), this.network);

                    var res = await this.consensusManager.BlockMinedAsync(block);
                    if (res == null)
                        throw new InvalidOperationException();

                    blocks.Add(block);
                }

                // Just to make sure we can still make simple blocks
                this.newBlock = AssemblerForTest(this).Build(this.chain.Tip, this.scriptPubKey);
                Assert.NotNull(this.newBlock);
            }

            private void InitializeSmartContractComponents(string callingMethod)
            {
                this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;

                this.Folder = TestBase.AssureEmptyDir(Path.Combine(AppContext.BaseDirectory, "TestCase", callingMethod));
                var engine = new DBreezeEngine(Path.Combine(this.Folder, "contracts"));
                var byteStore = new DBreezeByteStore(engine, "ContractState1");
                byteStore.Empty();
                ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(byteStore);

                this.StateRoot = new StateRepositoryRoot(stateDB);
                this.validator = new SmartContractValidator();

                this.refundProcessor = new ContractRefundProcessor(this.loggerFactory);
                this.transferProcessor = new ContractTransferProcessor(this.loggerFactory, this.network);

                this.AddressGenerator = new AddressGenerator();
                this.assemblyLoader = new ContractAssemblyLoader();
                this.callDataSerializer = new CallDataSerializer(new ContractPrimitiveSerializer(this.network));
                this.moduleDefinitionReader = new ContractModuleDefinitionReader();
                this.reflectionVirtualMachine = new ReflectionVirtualMachine(this.validator, this.loggerFactory, this.assemblyLoader, this.moduleDefinitionReader);
                this.stateProcessor = new StateProcessor(this.reflectionVirtualMachine, this.AddressGenerator);
                this.internalTxExecutorFactory = new InternalExecutorFactory(this.loggerFactory, this.stateProcessor);
                this.primitiveSerializer = new ContractPrimitiveSerializer(this.network);
                this.serializer = new Serializer(this.primitiveSerializer);
                this.smartContractStateFactory = new SmartContractStateFactory(this.primitiveSerializer, this.internalTxExecutorFactory, this.serializer);
                this.stateFactory = new StateFactory(this.smartContractStateFactory);
                this.ExecutorFactory = new ReflectionExecutorFactory(this.loggerFactory, this.callDataSerializer, this.refundProcessor, this.transferProcessor, this.stateFactory, this.stateProcessor, this.primitiveSerializer);
            }
        }

        /// <summary>
        /// Tests creation of a simple token contract
        /// </summary>
        [Fact]
        public async Task SmartContracts_CreateTokenContract_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas) SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Token.cs");
            Assert.True(compilationResult.Success);

            var contractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, contractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            byte[] ownerFromStorage = context.StateRoot.GetStorageValue(newContractAddress, Encoding.UTF8.GetBytes("Owner"));
            byte[] ownerToBytes = context.PrivateKey.PubKey.GetAddress(context.network).Hash.ToBytes();
            Assert.Equal(ownerFromStorage, ownerToBytes);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
            Assert.True(blockTemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
        }

        /// <summary>
        /// Try and spend outputs we don't own
        /// </summary>
        [Fact]
        public async Task SmartContracts_TrySpendingFundsThatArentOurs_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;

            var transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "Test");
            BlockTemplate pblocktemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate2.Block.Transactions.Count);

            context.mempool.Clear();

            var maliciousPerson = new Key();
            var entryFee = 10000;
            TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();
            var maliciousTxBuilder = new TransactionBuilder(context.network);
            var maliciousAmount = 500000000; // 5 BTC
            var maliciousPaymentScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(maliciousPerson.PubKey);

            maliciousTxBuilder.AddCoins(pblocktemplate2.Block.Transactions[2]);
            maliciousTxBuilder.Send(maliciousPaymentScript, maliciousAmount);
            maliciousTxBuilder.SetChange(context.PrivateKey);
            maliciousTxBuilder.SendFees(entryFee);
            var maliciousTx = maliciousTxBuilder.BuildTransaction(false);

            // Signing example
            //tx.Sign(new Key[] { context.privateKey }, funds);

            context.mempool.AddUnchecked(
                maliciousTx.GetHash(),
                entry.Fee(entryFee)
                    .Time(DateTimeProvider.Default.GetTime())
                    .SpendsCoinbase(true)
                    .FromTx(maliciousTx));

            await Assert.ThrowsAsync<ConsensusException>(async () =>
            {
                await this.BuildBlockAsync(context);
            });
        }

        /// <summary>
        /// Test that contracts correctly send funds to one person
        /// </summary>
        [Fact]
        public async Task SmartContracts_TransferFundsToSingleRecipient_Async()
        {
            var context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;

            var transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "Test");
            BlockTemplate pblocktemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate2.Block.Transactions.Count);
            Assert.True(pblocktemplate2.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Single(pblocktemplate2.Block.Transactions[2].Inputs); // There is 1 input to the condensing transaction: the previous callcontract transaction
            uint256 hashOfContractCallTx = pblocktemplate2.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfContractCallTx, pblocktemplate2.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend - 100, (ulong)pblocktemplate2.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the input - 100
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100

            context.mempool.Clear();

            var transferContractTxData2 = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "Test");
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData2, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate3.Block.Transactions.Count); // 1 coinbase, 1 contract call, 1 condensingtx with send
            Assert.True(pblocktemplate3.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Equal(2, pblocktemplate3.Block.Transactions[2].Inputs.Count); // There are 2 inputs to the condensing transaction: the previous callcontract transaction and the unspent from above
            uint256 hashOfPrevCondensingTx = pblocktemplate2.Block.Transactions[2].GetHash();
            uint256 hashOfContractCallTx3 = pblocktemplate3.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfPrevCondensingTx, pblocktemplate3.Block.Transactions[2].Inputs[1].PrevOut.Hash);
            Assert.Equal(hashOfContractCallTx3, pblocktemplate3.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend * 2 - 200, (ulong)pblocktemplate3.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the value given twice, - 200 (100 for each transfer)
            Assert.Equal(100, pblocktemplate3.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100
        }

        /// <summary>
        /// Send funds with create
        /// </summary>
        [Fact]
        public async Task SmartContracts_CreateWithFunds_Success_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas) SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
            Assert.True(compilationResult.Success);

            var contractCarrier = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, contractCarrier, context.txFirst[0].GetHash(), 100_000_000, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
            Assert.True(blockTemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            var unspent = context.StateRoot.GetUnspent(newContractAddress);
            context.mempool.Clear();
        }

        /// <summary>
        /// Test that contract correctly send funds to 2 people inside one contract call
        /// </summary>
        [Fact]
        public async Task SmartContracts_TransferFundsToMultipleRecipients_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            var contractCarrier = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, contractCarrier, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
            Assert.True(blockTemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;

            var transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "Test2");
            BlockTemplate blockTemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, blockTemplate2.Block.Transactions.Count);
            Assert.True(blockTemplate2.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Single(blockTemplate2.Block.Transactions[2].Inputs); // There is 1 input to the condensing transaction: the previous callcontract transaction
            var hashOfContractCallTx = blockTemplate2.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfContractCallTx, blockTemplate2.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend - 200, (ulong)blockTemplate2.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the input - 200
            Assert.Equal(100, blockTemplate2.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100
            Assert.Equal(100, blockTemplate2.Block.Transactions[2].Outputs[2].Value); // Third txout should be the transfer to a new person, with a value of 100

            context.mempool.Clear();

            transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "Test2");
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate3.Block.Transactions.Count); // 1 coinbase, 1 contract call, 1 condensingtx with send
            Assert.True(pblocktemplate3.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Equal(2, pblocktemplate3.Block.Transactions[2].Inputs.Count); // There are 2 inputs to the condensing transaction: the previous callcontract transaction and the unspent from above
            var hashOfPrevCondensingTx = blockTemplate2.Block.Transactions[2].GetHash();
            var hashOfContractCallTx3 = pblocktemplate3.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfPrevCondensingTx, pblocktemplate3.Block.Transactions[2].Inputs[1].PrevOut.Hash);
            Assert.Equal(hashOfContractCallTx3, pblocktemplate3.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend * 2 - 400, (ulong)pblocktemplate3.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the value given twice, - 400 (100 for each transfer)
            Assert.Equal(100, pblocktemplate3.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100
            Assert.Equal(100, pblocktemplate3.Block.Transactions[2].Outputs[2].Value); // Third txout should be the transfer to a new person, with a value of 100
        }

        /// <summary>
        /// Tests that contracts manage their UTXOs correctly when not sending funds or receiving funds.
        /// </summary>
        [Fact]
        public async Task SmartContracts_SendValue_NoTransfers_Async()
        {
            var context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas) SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
            Assert.True(blockTemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            var transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "DoNothing");
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[2].GetHash(), 100_000, gasBudget);
            Assert.Equal(2, pblocktemplate3.Block.Transactions.Count); // In this case we are sending 0, and doing no transfers, so we don't need a condensing transaction
        }

        /// <summary>
        /// Tests that contracts manage their UTXOs correctly when not sending funds or receiving funds.
        /// </summary>
        [Fact]
        public async Task SmartContracts_NoTransfers_Async()
        {
            var context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;
            var transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "Test2");
            BlockTemplate pblocktemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate2.Block.Transactions.Count);
            Assert.True(pblocktemplate2.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Single(pblocktemplate2.Block.Transactions[2].Inputs); // There is 1 input to the condensing transaction: the previous callcontract transaction
            var hashOfContractCallTx = pblocktemplate2.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfContractCallTx, pblocktemplate2.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend - 200, (ulong)pblocktemplate2.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the input - 200
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[2].Value); // Third txout should be the transfer to a new person, with a value of 100

            context.mempool.Clear();

            transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "DoNothing");
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[2].GetHash(), 0, gasBudget);
            Assert.Equal(2, pblocktemplate3.Block.Transactions.Count); // In this case we are sending 0, and doing no transfers, so we don't need a condensing transaction
        }

        /// <summary>
        /// Should deploy 2 contracts, and then send funds from one to the other and end up with correct balances for all.
        /// </summary>
        [Fact]
        public async Task SmartContracts_TransferToP2PKH_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            // This uses a lot of gas
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            ulong fundsToSend = 1000;

            var transferContractTxData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress, "P2KTest");
            blockTemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractTxData, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, blockTemplate.Block.Transactions.Count);
            Assert.Single(blockTemplate.Block.Transactions[2].Inputs);
            Assert.Equal(blockTemplate.Block.Transactions[1].GetHash(), blockTemplate.Block.Transactions[2].Inputs[0].PrevOut.Hash); // Input should be from the call that was just made.
            Assert.Equal(900, blockTemplate.Block.Transactions[2].Outputs[0].Value); // First txout should be the change back to the contract, with a value of 900
            Assert.Equal(100, blockTemplate.Block.Transactions[2].Outputs[1].Value); // First txout should be the transfer to the second contract, with a value of 100
        }

        /// <summary>
        /// Should deploy 2 contracts, and then send funds from one to the other and end up with correct balances for all.
        /// </summary>
        [Fact]
        public async Task SmartContracts_TransferBetweenContracts_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            // This uses a lot of gas
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/InterContract1.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            compilationResult = ContractCompiler.CompileFile("SmartContracts/InterContract2.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData2 = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            tx = this.AddTransactionToMempool(context, createContractTxData2, context.txFirst[1].GetHash(), 0, gasBudget);
            blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            object[] testMethodParameters =  { newContractAddress.ToAddress() };
            
            var transferContractCall = new ContractTxData(1, gasPrice, gasLimit, newContractAddress2, "ContractTransfer", testMethodParameters);
            blockTemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractCall, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(Encoding.UTF8.GetBytes("testString"), context.StateRoot.GetStorageValue(newContractAddress, Encoding.UTF8.GetBytes("test")));
            Assert.Equal(3, blockTemplate.Block.Transactions.Count);
            Assert.Single(blockTemplate.Block.Transactions[2].Inputs);
            Assert.Equal(blockTemplate.Block.Transactions[1].GetHash(), blockTemplate.Block.Transactions[2].Inputs[0].PrevOut.Hash); // Input should be from the call that was just made.
            Assert.Equal(900, blockTemplate.Block.Transactions[2].Outputs[0].Value); // First txout should be the change back to the contract, with a value of 900
            Assert.Equal(100, blockTemplate.Block.Transactions[2].Outputs[1].Value); // First txout should be the transfer to the second contract, with a value of 100

            context.mempool.Clear();

            blockTemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractCall, context.txFirst[3].GetHash(), 0, gasBudget);
            Assert.Equal(3, blockTemplate.Block.Transactions.Count);
            Assert.Equal(2, blockTemplate.Block.Transactions[2].Inputs.Count);
            Assert.Equal(800, blockTemplate.Block.Transactions[2].Outputs[0].Value);
            Assert.Equal(200, blockTemplate.Block.Transactions[2].Outputs[1].Value);
        }

        /// <summary>
        /// Should deploy 2 contracts, invoke a method on one, get the value from it, and persist it
        /// </summary>
        [Fact]
        public async Task SmartContracts_InvokeContract_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            // This uses a lot of gas
            var gasLimit = (RuntimeObserver.Gas) SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            var compilationResult = ContractCompiler.CompileFile("SmartContracts/CountContract.cs");
            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);

            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            compilationResult = ContractCompiler.CompileFile("SmartContracts/CallContract.cs");
            var createContractTxData2 = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            tx = this.AddTransactionToMempool(context, createContractTxData2, context.txFirst[1].GetHash(), 0, gasBudget);
            blockTemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            object[] testMethodParameters =  { newContractAddress.ToAddress() };

            var transferContractCallData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress2, "Tester", testMethodParameters);
            blockTemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractCallData, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            byte[] stateSaveValue = context.StateRoot.GetStorageValue(newContractAddress, Encoding.UTF8.GetBytes("SaveWorked"));
            Assert.NotNull(stateSaveValue);
            Assert.Single(stateSaveValue);
            Assert.True(Convert.ToBoolean(stateSaveValue[0]));
        }

        [Fact]
        public async Task SmartContracts_TransferBetweenContracts_WithException_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            // This uses a lot of gas
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/InterContract1.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);

            Transaction tx = AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);

            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            compilationResult = ContractCompiler.CompileFile("SmartContracts/InterContract2.cs");
            Assert.True(compilationResult.Success);

            var createContractTxData2 = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);

            tx = AddTransactionToMempool(context, createContractTxData2, context.txFirst[1].GetHash(), 0, gasBudget);
            blockTemplate = await BuildBlockAsync(context);
            uint160 newContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);

            Assert.NotNull(context.StateRoot.GetCode(newContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            object[] testMethodParameters = { newContractAddress.ToString() };

            var transferContractCallData = new ContractTxData(1, gasPrice, gasLimit, newContractAddress2, "ContractTransferWithFail", testMethodParameters);
            blockTemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractCallData, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, blockTemplate.Block.Transactions.Count);
            Assert.Single(blockTemplate.Block.Transactions[2].Inputs);
            Assert.Equal(blockTemplate.Block.Transactions[1].GetHash(), blockTemplate.Block.Transactions[2].Inputs[0].PrevOut.Hash); // Input should be from the call that was just made.
            Assert.Equal(1000, blockTemplate.Block.Transactions[2].Outputs[0].Value); // Only txOut should be to contract
        }

        /// <summary>
        /// Can execute a smart contract transaction referencing a P2PKH that's in the same block, above it.
        /// </summary>
        [Fact]
        public async Task SmartContract_ReferencingInputInSameBlock()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            // Create the transaction to be used as the input and add to mempool
            var preTransaction = context.network.Consensus.ConsensusFactory.CreateTransaction();
            var txIn = new TxIn(new OutPoint(context.txFirst[0].GetHash(), 0))
            {
                ScriptSig = context.PrivateKey.ScriptPubKey
            };
            preTransaction.AddInput(txIn);
            preTransaction.AddOutput(new TxOut(new Money(49, MoneyUnit.BTC), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(context.PrivateKey.PubKey)));
            preTransaction.Sign(context.network, context.PrivateKey, false);

            var entry = new TestMemPoolEntryHelper();
            context.mempool.AddUnchecked(preTransaction.GetHash(), entry.Fee(30000).Time(DateTimeProvider.Default.GetTime()).SpendsCoinbase(true).FromTx(preTransaction));

            // Add the smart contract transaction to the mempool and mine as normal.
            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas) SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/InterContract1.cs");
            Assert.True(compilationResult.Success);
            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, compilationResult.Compilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, preTransaction.GetHash(), 0, gasBudget, false);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);

            // Check all went well. i.e. contract is deployed.
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(newContractAddress));
        }

        /// <summary>
        /// Should send funds to another contract, causing the contract's ReceiveHandler function to be invoked.
        /// </summary>
        [Fact]
        public async Task SmartContracts_TransferFunds_Invokes_Receive_Async()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            ulong gasPrice = 1;
            var gasLimit = (RuntimeObserver.Gas)SmartContractFormatLogic.GasLimitMaximum;
            var gasBudget = gasPrice * gasLimit;

            var receiveContract = Path.Combine("SmartContracts", "ReceiveHandlerContract.cs");
            var receiveCompilation = ContractCompiler.CompileFile(receiveContract).Compilation;

            var createContractTxData = new ContractTxData(1, gasPrice, gasLimit, receiveCompilation);
            Transaction tx = this.AddTransactionToMempool(context, createContractTxData, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate blockTemplate = await this.BuildBlockAsync(context);
            uint160 receiveContractAddress1 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(receiveContractAddress1));

            context.mempool.Clear();

            var createContractTxData2 = new ContractTxData(1, gasPrice, gasLimit, receiveCompilation);
            tx = this.AddTransactionToMempool(context, createContractTxData2, context.txFirst[1].GetHash(), 0, gasBudget);
            blockTemplate = await this.BuildBlockAsync(context);
            uint160 receiveContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.StateRoot.GetCode(receiveContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            object[] testMethodParameters = { receiveContractAddress2.ToAddress(), fundsToSend };
            
            var transferContractCallData = new ContractTxData(1, gasPrice, gasLimit, receiveContractAddress1, "SendFunds", testMethodParameters);

            blockTemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferContractCallData, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            byte[] receiveInvoked = context.StateRoot.GetStorageValue(receiveContractAddress2, Encoding.UTF8.GetBytes("ReceiveInvoked"));
            byte[] fundsReceived = context.StateRoot.GetStorageValue(receiveContractAddress2, Encoding.UTF8.GetBytes("ReceivedFunds"));

            var serializer = new ContractPrimitiveSerializer(context.network);

            Assert.NotNull(receiveInvoked);
            Assert.NotNull(fundsReceived);
            Assert.True(serializer.Deserialize<bool>(receiveInvoked));
            Assert.Equal(fundsToSend, serializer.Deserialize<ulong>(fundsReceived));
        }

        private async Task<BlockTemplate> AddTransactionToMemPoolAndBuildBlockAsync(TestContext context, ContractTxData contractTxData, uint256 prevOutHash, ulong value, ulong gasBudget)
        {
            this.AddTransactionToMempool(context, contractTxData, prevOutHash, value, gasBudget);
            return await this.BuildBlockAsync(context);
        }

        private Transaction AddTransactionToMempool(TestContext context, ContractTxData contractTxData, uint256 prevOutHash, ulong value, ulong gasBudget, bool spendsCoinbase = true)
        {
            var entryFee = gasBudget;
            TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();
            Transaction tx = new Transaction();
            var txIn = new TxIn(new OutPoint(prevOutHash, 0))
            {
                ScriptSig = context.PrivateKey.ScriptPubKey
            };
            tx.AddInput(txIn);
            tx.AddOutput(new TxOut(new Money(value), new Script(context.callDataSerializer.Serialize(contractTxData))));
            tx.Sign(context.network, context.PrivateKey, false);
            context.mempool.AddUnchecked(tx.GetHash(), entry.Fee(entryFee).Time(DateTimeProvider.Default.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx));
            return tx;
        }

        private async Task<BlockTemplate> BuildBlockAsync(TestContext context)
        {
            BlockTemplate blockTemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);

            while (!blockTemplate.Block.CheckProofOfWork())
                blockTemplate.Block.Header.Nonce = ++context.Nonce;

            await context.consensusManager.BlockMinedAsync(blockTemplate.Block);
            return blockTemplate;
        }
    }
}