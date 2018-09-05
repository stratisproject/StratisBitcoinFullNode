using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DBreeze;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.IntegrationTests.Mempool;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Loader;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;
using Key = NBitcoin.Key;


namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    /// <summary>
    /// This is taken from 'MinerTests.cs' and adjusted to use a different block validator.
    /// </summary>
    public sealed class SmartContractMinerTests
    {
        private static FeeRate blockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);

        public static BlockDefinition AssemblerForTest(TestContext testContext)
        {
            return new SmartContractBlockDefinition(
                new BlockBufferGenerator(),
                testContext.cachedCoinView,
                testContext.consensus,
                testContext.date,
                testContext.executorFactory,
                new LoggerFactory(),
                testContext.mempool,
                testContext.mempoolLock,
                new MinerSettings(testContext.nodeSettings),
                testContext.network,
                new SenderRetriever(),
                testContext.stateRoot);
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
            public Network network;
            public NodeSettings nodeSettings;
            public Script scriptPubKey;
            public BlockTemplate newBlock;
            public Transaction tx, tx2;
            public Script script;
            public uint256 hash;
            public TestMemPoolEntryHelper entry;
            public ConcurrentChain chain;
            public ConsensusLoop consensus;
            public DateTimeProvider date;
            public TxMempool mempool;
            public MempoolSchedulerLock mempoolLock;
            public List<Transaction> txFirst;
            public Money BLOCKSUBSIDY = 50 * Money.COIN;
            public Money LOWFEE = Money.CENT;
            public Money HIGHFEE = Money.COIN;
            public Money HIGHERFEE = 4 * Money.COIN;
            public int baseheight;
            public CachedCoinView cachedCoinView;
            public ISmartContractResultRefundProcessor refundProcessor;
            public ContractStateRoot stateRoot;
            public ISmartContractResultTransferProcessor transferProcessor;
            public SmartContractValidator validator;
            public IKeyEncodingStrategy keyEncodingStrategy;
            public ReflectionSmartContractExecutorFactory executorFactory;

            private bool useCheckpoints = true;
            public Key privateKey;
            private InternalTransactionExecutorFactory internalTxExecutorFactory;
            private ReflectionVirtualMachine vm;
            private ICallDataSerializer serializer;
            private ContractAssemblyLoader assemblyLoader;
            private IContractModuleDefinitionReader moduleDefinitionReader;
            private IContractPrimitiveSerializer contractPrimitiveSerializer;
            private StateFactory stateFactory;
            public AddressGenerator AddressGenerator { get; set; }

            public TestContext()
            {
            }

            public async Task InitializeAsync([CallerMemberName] string callingMethod = "")
            {
                this.blockinfo = new List<Blockinfo>();
                var lst = blockinfoarr.Cast<long>().ToList();
                for (int i = 0; i < lst.Count; i += 2)
                    this.blockinfo.Add(new Blockinfo { extranonce = (int)lst[i], nonce = (uint)lst[i + 1] });

                // Note that by default, these tests run with size accounting enabled.
                this.network = new SmartContractsRegTest();
                this.privateKey = new Key();
                this.scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(this.privateKey.PubKey);
                this.newBlock = new BlockTemplate(this.network);

                this.entry = new TestMemPoolEntryHelper();
                this.chain = new ConcurrentChain(this.network);
                this.network.Consensus.Options = new ConsensusOptions();
                this.network.Consensus.Rules = new SmartContractPowRuleRegistration().GetRules();

                IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;

                this.cachedCoinView = new CachedCoinView(new InMemoryCoinView(this.chain.Tip.HashBlock), dateTimeProvider, new LoggerFactory());

                var loggerFactory = new ExtendedLoggerFactory();
                loggerFactory.AddConsoleWithFilters();

                this.nodeSettings = NodeSettings.Default();
                var consensusSettings = new ConsensusSettings(this.nodeSettings);
                consensusSettings.UseCheckpoints = this.useCheckpoints;

                this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;

                var folder = TestBase.AssureEmptyDir(Path.Combine(AppContext.BaseDirectory, "TestData", callingMethod));

                var engine = new DBreezeEngine(folder);
                var byteStore = new DBreezeByteStore(engine, "ContractState1");
                byteStore.Empty();
                ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(byteStore);

                this.stateRoot = new ContractStateRoot(stateDB);
                this.validator = new SmartContractValidator();

                this.refundProcessor = new SmartContractResultRefundProcessor(loggerFactory);
                this.transferProcessor = new SmartContractResultTransferProcessor(loggerFactory, this.network);

                this.serializer = CallDataSerializer.Default;
                this.AddressGenerator = new AddressGenerator();
                this.assemblyLoader = new ContractAssemblyLoader();
                this.moduleDefinitionReader = new ContractModuleDefinitionReader();
                this.contractPrimitiveSerializer = new ContractPrimitiveSerializer(this.network);
                this.vm = new ReflectionVirtualMachine(this.validator, loggerFactory, this.network, this.assemblyLoader, this.moduleDefinitionReader);
                this.internalTxExecutorFactory = new InternalTransactionExecutorFactory(loggerFactory, this.network);
                this.stateFactory = new StateFactory(this.network, this.contractPrimitiveSerializer, this.vm, this.AddressGenerator, this.internalTxExecutorFactory);
                this.executorFactory = new ReflectionSmartContractExecutorFactory(loggerFactory, this.serializer, this.refundProcessor, this.transferProcessor, this.network, this.stateFactory);

                var networkPeerFactory = new NetworkPeerFactory(this.network, dateTimeProvider, loggerFactory, new PayloadProvider(), new SelfEndpointTracker(loggerFactory), new Mock<IInitialBlockDownloadState>().Object, new ConnectionManagerSettings());
                var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, nodeSettings.DataFolder, loggerFactory, new SelfEndpointTracker(loggerFactory));

                var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(loggerFactory), loggerFactory, this.network, networkPeerFactory, new NodeLifetime(), nodeSettings, peerAddressManager);
                var connectionSettings = new ConnectionManagerSettings(nodeSettings);
                var selfEndpointTracker = new SelfEndpointTracker(loggerFactory);
                var connectionManager = new ConnectionManager(dateTimeProvider, loggerFactory, this.network, networkPeerFactory, nodeSettings, new NodeLifetime(), new NetworkPeerConnectionParameters(), peerAddressManager, new IPeerConnector[] { }, peerDiscovery, selfEndpointTracker, connectionSettings, new SmartContractVersionProvider());
                var blockPuller = new LookaheadBlockPuller(this.chain, connectionManager, new LoggerFactory());
                var peerBanning = new PeerBanning(connectionManager, loggerFactory, dateTimeProvider, peerAddressManager);
                var nodeDeployments = new NodeDeployments(this.network, this.chain);
                var senderRetriever = new SenderRetriever();

                var smartContractRuleRegistration = new SmartContractPowRuleRegistration();

                ConsensusRules consensusRules = new SmartContractPowConsensusRuleEngine(this.chain, new Checkpoints(), consensusSettings, dateTimeProvider, this.executorFactory, loggerFactory, this.network, nodeDeployments, this.stateRoot, blockPuller, new PersistentReceiptRepository(new DataFolder(folder)), senderRetriever, this.cachedCoinView).Register();

                this.consensus = new ConsensusLoop(new AsyncLoopFactory(loggerFactory), new NodeLifetime(), this.chain, this.cachedCoinView, blockPuller, new NodeDeployments(this.network, this.chain), loggerFactory, new ChainState(new InvalidBlockHashStore(dateTimeProvider)), connectionManager, dateTimeProvider, new Signals.Signals(), consensusSettings, this.nodeSettings, peerBanning, consensusRules);
                await this.consensus.StartAsync();

                this.entry.Fee(11);
                this.entry.Height(11);
                var date1 = new MemoryPoolTests.DateTimeProviderSet();
                date1.time = dateTimeProvider.GetTime();
                date1.timeutc = dateTimeProvider.GetUtcNow();
                this.date = date1;
                this.mempool = new TxMempool(dateTimeProvider, new BlockPolicyEstimator(new MempoolSettings(this.nodeSettings), new LoggerFactory(), this.nodeSettings), new LoggerFactory(), this.nodeSettings); ;
                this.mempoolLock = new MempoolSchedulerLock();

                // Simple block creation, nothing special yet:
                this.newBlock = AssemblerForTest(this).Build(this.chain.Tip, this.scriptPubKey);
                this.chain.SetTip(this.newBlock.Block.Header);
                await this.consensus.ValidateAndExecuteBlockAsync(new PowRuleContext(new ValidationContext { Block = this.newBlock.Block }, this.network.Consensus, this.consensus.Tip, dateTimeProvider.GetTimeOffset()) { MinedBlock = true });

                // We can't make transactions until we have inputs
                // Therefore, load 100 blocks :)
                this.baseheight = 0;
                List<NBitcoin.Block> blocks = new List<NBitcoin.Block>();
                this.txFirst = new List<Transaction>();
                for (int i = 0; i < this.blockinfo.Count; ++i)
                {
                    var block = NBitcoin.Block.Load(this.newBlock.Block.ToBytes(this.network.Consensus.ConsensusFactory), this.network);
                    ((SmartContractBlockHeader)block.Header).HashStateRoot = ((SmartContractBlockHeader)this.newBlock.Block.Header).HashStateRoot;
                    block.Header.HashPrevBlock = this.chain.Tip.HashBlock;
                    block.Header.Version = 1;
                    block.Header.Time = Utils.DateTimeToUnixTime(this.chain.Tip.GetMedianTimePast()) + 1;

                    Transaction txCoinbase = this.network.CreateTransaction(block.Transactions[0].ToBytes(this.network.Consensus.ConsensusFactory));
                    txCoinbase.Inputs.Clear();
                    txCoinbase.Version = 1;
                    txCoinbase.AddInput(new TxIn(new Script(new[] { Op.GetPushOp(this.blockinfo[i].extranonce), Op.GetPushOp(this.chain.Height) })));
                    txCoinbase.AddOutput(new TxOut(Money.Zero, new Script()));
                    block.Transactions[0] = txCoinbase;

                    if (this.txFirst.Count == 0)
                        this.baseheight = this.chain.Height;

                    if (this.txFirst.Count < 4)
                        this.txFirst.Add(block.Transactions[0]);

                    block.UpdateMerkleRoot();

                    block.Header.Nonce = this.blockinfo[i].nonce;

                    this.chain.SetTip(block.Header);
                    await this.consensus.ValidateAndExecuteBlockAsync(new PowRuleContext(new ValidationContext { Block = block }, this.network.Consensus, this.consensus.Tip, dateTimeProvider.GetTimeOffset()) { MinedBlock = true });
                    blocks.Add(block);
                }

                // Just to make sure we can still make simple blocks
                this.newBlock = AssemblerForTest(this).Build(this.chain.Tip, this.scriptPubKey);
                Assert.NotNull(this.newBlock);
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/Token.cs");
            Assert.True(compilationResult.Success);

            var smartContractCarrier = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, smartContractCarrier, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            byte[] ownerFromStorage = context.stateRoot.GetStorageValue(newContractAddress, Encoding.UTF8.GetBytes("Owner"));
            byte[] ownerToBytes = context.privateKey.PubKey.GetAddress(context.network).Hash.ToBytes();
            Assert.Equal(ownerFromStorage, ownerToBytes);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;

            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "Test", gasPrice, gasLimit);
            BlockTemplate pblocktemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
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
            maliciousTxBuilder.SetChange(context.privateKey);
            maliciousTxBuilder.SendFees(entryFee);
            var maliciousTx = maliciousTxBuilder.BuildTransaction(false);

            // Signing example
            //tx.Sign(new Key[] { context.privateKey }, funds);

            context.mempool.AddUnchecked(
                maliciousTx.GetHash(),
                entry.Fee(entryFee)
                    .Time(context.date.GetTime())
                    .SpendsCoinbase(true)
                    .FromTx(maliciousTx));

            await Assert.ThrowsAsync<ConsensusErrorException>(async () =>
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;

            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "Test", gasPrice, gasLimit);
            BlockTemplate pblocktemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate2.Block.Transactions.Count);
            Assert.True(pblocktemplate2.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Single(pblocktemplate2.Block.Transactions[2].Inputs); // There is 1 input to the condensing transaction: the previous callcontract transaction
            uint256 hashOfContractCallTx = pblocktemplate2.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfContractCallTx, pblocktemplate2.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend - 100, (ulong)pblocktemplate2.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the input - 100
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100

            context.mempool.Clear();

            SmartContractCarrier transferTransaction2 = SmartContractCarrier.CallContract(1, newContractAddress, "Test", gasPrice, gasLimit);
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction2, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
            Assert.True(compilationResult.Success);

            var contractCarrier = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractCarrier, context.txFirst[0].GetHash(), 100_000_000, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            var unspent = context.stateRoot.GetUnspent(newContractAddress);
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            var contractCarrier = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractCarrier, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;

            var transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "Test2", gasPrice, gasLimit);
            BlockTemplate pblocktemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate2.Block.Transactions.Count);
            Assert.True(pblocktemplate2.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Single(pblocktemplate2.Block.Transactions[2].Inputs); // There is 1 input to the condensing transaction: the previous callcontract transaction
            var hashOfContractCallTx = pblocktemplate2.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfContractCallTx, pblocktemplate2.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend - 200, (ulong)pblocktemplate2.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the input - 200
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[2].Value); // Third txout should be the transfer to a new person, with a value of 100

            context.mempool.Clear();

            transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "Test2", gasPrice, gasLimit);
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate3.Block.Transactions.Count); // 1 coinbase, 1 contract call, 1 condensingtx with send
            Assert.True(pblocktemplate3.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Equal(2, pblocktemplate3.Block.Transactions[2].Inputs.Count); // There are 2 inputs to the condensing transaction: the previous callcontract transaction and the unspent from above
            var hashOfPrevCondensingTx = pblocktemplate2.Block.Transactions[2].GetHash();
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            var transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "DoNothing", gasPrice, gasLimit);
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[2].GetHash(), 100_000, gasBudget);
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
            Assert.True(pblocktemplate.Block.Transactions[0].Outputs[1].Value > 0); // gas refund

            context.mempool.Clear();

            ulong fundsToSend = 5000000000L - gasBudget;
            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "Test2", gasPrice, gasLimit);
            BlockTemplate pblocktemplate2 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate2.Block.Transactions.Count);
            Assert.True(pblocktemplate2.Block.Transactions[0].Outputs[1].Value > 0); // gas refund
            Assert.Single(pblocktemplate2.Block.Transactions[2].Inputs); // There is 1 input to the condensing transaction: the previous callcontract transaction
            var hashOfContractCallTx = pblocktemplate2.Block.Transactions[1].GetHash();
            Assert.Equal(hashOfContractCallTx, pblocktemplate2.Block.Transactions[2].Inputs[0].PrevOut.Hash);
            Assert.Equal(fundsToSend - 200, (ulong)pblocktemplate2.Block.Transactions[2].Outputs[0].Value); // First txout should be the change to the contract, with a value of the input - 200
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[1].Value); // Second txout should be the transfer to a new person, with a value of 100
            Assert.Equal(100, pblocktemplate2.Block.Transactions[2].Outputs[2].Value); // Third txout should be the transfer to a new person, with a value of 100

            context.mempool.Clear();

            transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "DoNothing", gasPrice, gasLimit);
            BlockTemplate pblocktemplate3 = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[2].GetHash(), 0, gasBudget);
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            ulong fundsToSend = 1000;

            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress, "P2KTest", gasPrice, gasLimit);
            pblocktemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[1].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate.Block.Transactions.Count);
            Assert.Single(pblocktemplate.Block.Transactions[2].Inputs);
            Assert.Equal(pblocktemplate.Block.Transactions[1].GetHash(), pblocktemplate.Block.Transactions[2].Inputs[0].PrevOut.Hash); // Input should be from the call that was just made.
            Assert.Equal(900, pblocktemplate.Block.Transactions[2].Outputs[0].Value); // First txout should be the change back to the contract, with a value of 900
            Assert.Equal(100, pblocktemplate.Block.Transactions[2].Outputs[1].Value); // First txout should be the transfer to the second contract, with a value of 100
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/InterContract1.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            compilationResult = SmartContractCompiler.CompileFile("SmartContracts/InterContract2.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction2 = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            tx = this.AddTransactionToMempool(context, contractTransaction2, context.txFirst[1].GetHash(), 0, gasBudget);
            pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            string[] testMethodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, newContractAddress.ToAddress(context.network)),
            };

            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress2, "ContractTransfer", gasPrice, gasLimit, testMethodParameters);
            pblocktemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(Encoding.UTF8.GetBytes("testString"), context.stateRoot.GetStorageValue(newContractAddress, Encoding.UTF8.GetBytes("test")));
            Assert.Equal(3, pblocktemplate.Block.Transactions.Count);
            Assert.Single(pblocktemplate.Block.Transactions[2].Inputs);
            Assert.Equal(pblocktemplate.Block.Transactions[1].GetHash(), pblocktemplate.Block.Transactions[2].Inputs[0].PrevOut.Hash); // Input should be from the call that was just made.
            Assert.Equal(900, pblocktemplate.Block.Transactions[2].Outputs[0].Value); // First txout should be the change back to the contract, with a value of 900
            Assert.Equal(100, pblocktemplate.Block.Transactions[2].Outputs[1].Value); // First txout should be the transfer to the second contract, with a value of 100

            context.mempool.Clear();

            SmartContractCarrier transferTransaction2 = SmartContractCarrier.CallContract(1, newContractAddress2, "ContractTransfer", gasPrice, gasLimit, testMethodParameters);
            pblocktemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction2, context.txFirst[3].GetHash(), 0, gasBudget);
            Assert.Equal(3, pblocktemplate.Block.Transactions.Count);
            Assert.Equal(2, pblocktemplate.Block.Transactions[2].Inputs.Count);
            Assert.Equal(800, pblocktemplate.Block.Transactions[2].Outputs[0].Value);
            Assert.Equal(200, pblocktemplate.Block.Transactions[2].Outputs[1].Value);
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, SmartContractCompiler.CompileFile("SmartContracts/CountContract.cs").Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            SmartContractCarrier contractTransaction2 = SmartContractCarrier.CreateContract(1, SmartContractCompiler.CompileFile("SmartContracts/CallContract.cs").Compilation, gasPrice, gasLimit);
            tx = this.AddTransactionToMempool(context, contractTransaction2, context.txFirst[1].GetHash(), 0, gasBudget);
            pblocktemplate = await this.BuildBlockAsync(context);
            uint160 newContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            string[] testMethodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, newContractAddress.ToAddress(context.network)),
            };

            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress2, "Tester", gasPrice, gasLimit, testMethodParameters);
            pblocktemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            byte[] stateSaveValue = context.stateRoot.GetStorageValue(newContractAddress, Encoding.UTF8.GetBytes("SaveWorked"));
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/InterContract1.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);

            Transaction tx = AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await BuildBlockAsync(context);
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);

            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));

            context.mempool.Clear();

            compilationResult = SmartContractCompiler.CompileFile("SmartContracts/InterContract2.cs");
            Assert.True(compilationResult.Success);

            SmartContractCarrier contractTransaction2 = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);

            tx = AddTransactionToMempool(context, contractTransaction2, context.txFirst[1].GetHash(), 0, gasBudget);
            pblocktemplate = await BuildBlockAsync(context);
            uint160 newContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);

            Assert.NotNull(context.stateRoot.GetCode(newContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            string[] testMethodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.String, newContractAddress.ToString()),
            };
            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, newContractAddress2, "ContractTransferWithFail", gasPrice, gasLimit, testMethodParameters);
            pblocktemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            Assert.Equal(3, pblocktemplate.Block.Transactions.Count);
            Assert.Single(pblocktemplate.Block.Transactions[2].Inputs);
            Assert.Equal(pblocktemplate.Block.Transactions[1].GetHash(), pblocktemplate.Block.Transactions[2].Inputs[0].PrevOut.Hash); // Input should be from the call that was just made.
            Assert.Equal(1000, pblocktemplate.Block.Transactions[2].Outputs[0].Value); // Only txOut should be to contract
        }

        /// <summary>
        /// Can execute a smart contract transaction referencing a P2PKH that's in the same block, above it.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SmartContract_ReferencingInputInSameBlock()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();

            // Create the transaction to be used as the input and add to mempool
            var preTransaction = context.network.Consensus.ConsensusFactory.CreateTransaction();
            var txIn = new TxIn(new OutPoint(context.txFirst[0].GetHash(), 0))
            {
                ScriptSig = context.privateKey.ScriptPubKey
            };
            preTransaction.AddInput(txIn);
            preTransaction.AddOutput(new TxOut(new Money(49, MoneyUnit.BTC), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(context.privateKey.PubKey)));
            preTransaction.Sign(context.network, context.privateKey, false);

            var entry = new TestMemPoolEntryHelper();
            context.mempool.AddUnchecked(preTransaction.GetHash(), entry.Fee(30000).Time(context.date.GetTime()).SpendsCoinbase(true).FromTx(preTransaction));

            // Add the smart contract transaction to the mempool and mine as normal.
            ulong gasPrice = 1;
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/InterContract1.cs");
            Assert.True(compilationResult.Success);
            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, compilationResult.Compilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, preTransaction.GetHash(), 0, gasBudget, false);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);

            // Check all went well. i.e. contract is deployed.
            uint160 newContractAddress = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(newContractAddress));
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
            Gas gasLimit = (Gas)1000000;
            var gasBudget = gasPrice * gasLimit;

            var receiveContract = Path.Combine("SmartContracts", "ReceiveHandlerContract.cs");
            var receiveCompilation = SmartContractCompiler.CompileFile(receiveContract).Compilation;

            SmartContractCarrier contractTransaction = SmartContractCarrier.CreateContract(1, receiveCompilation, gasPrice, gasLimit);
            Transaction tx = this.AddTransactionToMempool(context, contractTransaction, context.txFirst[0].GetHash(), 0, gasBudget);
            BlockTemplate pblocktemplate = await this.BuildBlockAsync(context);
            uint160 receiveContractAddress1 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(receiveContractAddress1));

            context.mempool.Clear();

            SmartContractCarrier contractTransaction2 = SmartContractCarrier.CreateContract(1, receiveCompilation, gasPrice, gasLimit);
            tx = this.AddTransactionToMempool(context, contractTransaction2, context.txFirst[1].GetHash(), 0, gasBudget);
            pblocktemplate = await this.BuildBlockAsync(context);
            uint160 receiveContractAddress2 = context.AddressGenerator.GenerateAddress(tx.GetHash(), 0);
            Assert.NotNull(context.stateRoot.GetCode(receiveContractAddress2));

            context.mempool.Clear();

            ulong fundsToSend = 1000;
            string[] testMethodParameters = new string[]
            {
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.Address, receiveContractAddress2.ToAddress(context.network)),
                string.Format("{0}#{1}", (int)SmartContractCarrierDataType.ULong, fundsToSend),
            };

            SmartContractCarrier transferTransaction = SmartContractCarrier.CallContract(1, receiveContractAddress1, "SendFunds", gasPrice, gasLimit, testMethodParameters);
            pblocktemplate = await this.AddTransactionToMemPoolAndBuildBlockAsync(context, transferTransaction, context.txFirst[2].GetHash(), fundsToSend, gasBudget);
            byte[] receiveInvoked = context.stateRoot.GetStorageValue(receiveContractAddress2, Encoding.UTF8.GetBytes("ReceiveInvoked"));
            byte[] fundsReceived = context.stateRoot.GetStorageValue(receiveContractAddress2, Encoding.UTF8.GetBytes("ReceivedFunds"));

            var serializer = new ContractPrimitiveSerializer(context.network);

            Assert.NotNull(receiveInvoked);
            Assert.NotNull(fundsReceived);
            Assert.True(serializer.Deserialize<bool>(receiveInvoked));
            Assert.Equal(fundsToSend, serializer.Deserialize<ulong>(fundsReceived));
        }

        private async Task<BlockTemplate> AddTransactionToMemPoolAndBuildBlockAsync(TestContext context, SmartContractCarrier smartContractCarrier, uint256 prevOutHash, ulong value, ulong gasBudget)
        {
            this.AddTransactionToMempool(context, smartContractCarrier, prevOutHash, value, gasBudget);
            return await this.BuildBlockAsync(context);
        }

        private Transaction AddTransactionToMempool(TestContext context, SmartContractCarrier smartContractCarrier, uint256 prevOutHash, ulong value, ulong gasBudget, bool spendsCoinbase = true)
        {
            var entryFee = gasBudget;
            TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();
            Transaction tx = new Transaction();
            var txIn = new TxIn(new OutPoint(prevOutHash, 0))
            {
                ScriptSig = context.privateKey.ScriptPubKey
            };
            tx.AddInput(txIn);
            tx.AddOutput(new TxOut(new Money(value), new Script(smartContractCarrier.Serialize())));
            tx.Sign(context.network, context.privateKey, false);
            context.mempool.AddUnchecked(tx.GetHash(), entry.Fee(entryFee).Time(context.date.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx));
            return tx;
        }

        private async Task<BlockTemplate> BuildBlockAsync(TestContext context)
        {
            BlockTemplate pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);
            context.chain.SetTip(pblocktemplate.Block.Header);
            await context.consensus.ValidateAndExecuteBlockAsync(new PowRuleContext(new ValidationContext { Block = pblocktemplate.Block }, context.network.Consensus, context.consensus.Tip, context.date.GetTimeOffset()) { MinedBlock = true });
            return pblocktemplate;
        }
    }

    public sealed class MockServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> registered;

        public MockServiceProvider(
            ICoinView coinView,
            ISmartContractExecutorFactory executorFactory,
            ContractStateRoot stateRoot,
            ILoggerFactory loggerFactory)
        {
            this.registered = new Dictionary<Type, object>
            {
                { typeof(ICoinView), coinView },
                { typeof(ISmartContractExecutorFactory), executorFactory },
                { typeof(ContractStateRoot), stateRoot },
                { typeof(ILoggerFactory), loggerFactory }
            };
        }

        public object GetService(Type serviceType)
        {
            return this.registered[serviceType];
        }
    }
}