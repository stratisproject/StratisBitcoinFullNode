using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    /// <summary>
    /// Unit tests for the memory pool validator.
    /// </summary>
    public class MempoolValidatorTest : TestBase
    {
        public MempoolValidatorTest() : base(KnownNetworks.RegTest)
        {
        }

        [Fact]
        public async void CheckFinalTransaction_WithElapsedLockTime_ReturnsTrueAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            tx.Inputs.First().Sequence = new Sequence(Sequence.SEQUENCE_LOCKTIME_DISABLE_FLAG);

            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), destSecret.PubKeyHash));

            // Do not set an nLockTime for this test.

            // Non-PoS chains do not have the concept of a transaction time, so do not set that.

            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.True(isSuccess, "Transaction with elapsed nLockTime should have been accepted.");
        }

        [Fact(Skip = "Not implemented yet.")]
        public void CheckSequenceLocks_WithExistingLockPointAndValidChain_ReturnsTrue()
        {
            // TODO: Test case- Check two cases, chain with/without previous block
            // No Previous - time of lock == 0
            // Previous - time of lock < tip chain median time
        }

        [Fact(Skip = "Not implemented yet.")]
        public void CheckSequenceLocks_WithExistingLockPointAndChainWithPrevAndExpiredBlockTime_Fails()
        {
            // TODO: Test case - The lock point MinTime exceeds chain previous block median time
        }

        [Fact(Skip = "Not implemented yet.")]
        public void CheckSequenceLocks_WithExistingLockPointAndChainWihtNoPrevAndExpiredBlockTime_Fails()
        {
            // TODO: Test case - the lock point MinTime exceeds 0
        }

        [Fact(Skip = "Not implemented yet.")]
        public void GetTransactionWeight_WitnessTx_ReturnsWeight()
        {
            // TODO: Test getting tx weight on transaction with witness
        }

        [Fact(Skip = "Not implemented yet.")]
        public void GetTransactionWeight_StandardTx_ReturnsWeight()
        {
            // TODO: Test getting tx weight on transaction without witness
        }

        [Fact(Skip = "Not implemented yet.")]
        public void CalculateModifiedSize_CalcWeightWithTxIns_ReturnsSize()
        {
            // TODO: Test GetTransactionWeight - InputSizes = CalculateModifiedSize
            // Test weight calculation by input nTxSize = 0
        }

        [Fact(Skip = "Not implemented yet.")]
        public void CalculateModifiedSize_PreCalcWeightWithTxIns_ReturnsSize()
        {
            // TODO: Test GetTransactionWeight - InputSizes = CalculateModifiedSize
            // Test weight calculation by passing in weight as nTxSize, nTxSize can be computed with GetTransactionWeight()
        }

        [Fact]
        public async Task AcceptToMemoryPool_WithValidP2PKHTxn_IsSuccessfulAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            var network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.CENT * 11), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.True(isSuccess, "P2PKH tx not valid.");
        }

        /// <summary>
        /// Validate multi input/output P2PK, P2PKH transactions in memory pool.
        /// Transaction scenario adapted from code project article referenced below.
        /// </summary>
        /// <seealso cref="https://www.codeproject.com/Articles/835098/NBitcoin-Build-Them-All"/>
        [Fact]
        public async Task AcceptToMemoryPool_WithMultiInOutValidTxns_IsSuccessfulAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            var miner = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            ITestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, miner.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var alice = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var satoshi = new BitcoinSecret(new Key(), KnownNetworks.RegTest);

            // Fund Alice, Bob, Satoshi
            // 50 Coins come from first tx on chain - send satoshi 1, bob 2, Alice 1.5 and change back to miner
            var coin = new Coin(context.SrcTxs[0].GetHash(), 0, context.SrcTxs[0].TotalOut, miner.ScriptPubKey);
            var txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction multiOutputTx = txBuilder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(miner)
                .Send(satoshi.GetAddress(), "1.00")
                .Send(bob.GetAddress(), "2.00")
                .Send(alice.GetAddress(), "1.50")
                .SendFees("0.001")
                .SetChange(miner.GetAddress())
                .BuildTransaction(true);
            Assert.True(txBuilder.Verify(multiOutputTx)); //check fully signed
            var state = new MempoolValidationState(false);
            Assert.True(await validator.AcceptToMemoryPool(state, multiOutputTx), $"Transaction: {nameof(multiOutputTx)} failed mempool validation.");

            // Alice then Bob sends to Satoshi
            Coin[] aliceCoins = multiOutputTx.Outputs
                        .Where(o => o.ScriptPubKey == alice.ScriptPubKey)
                        .Select(o => new Coin(new OutPoint(multiOutputTx.GetHash(), multiOutputTx.Outputs.IndexOf(o)), o))
                        .ToArray();
            Coin[] bobCoins = multiOutputTx.Outputs
                        .Where(o => o.ScriptPubKey == bob.ScriptPubKey)
                        .Select(o => new Coin(new OutPoint(multiOutputTx.GetHash(), multiOutputTx.Outputs.IndexOf(o)), o))
                        .ToArray();

            txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction multiInputTx = txBuilder
                .AddCoins(aliceCoins)
                .AddKeys(alice)
                .Send(satoshi.GetAddress(), "0.8")
                .SetChange(alice.GetAddress())
                .SendFees("0.0005")
                .Then()
                .AddCoins(bobCoins)
                .AddKeys(bob)
                .Send(satoshi.GetAddress(), "0.2")
                .SetChange(bob.GetAddress())
                .SendFees("0.0005")
                .BuildTransaction(true);
            Assert.True(txBuilder.Verify(multiInputTx)); //check fully signed
            Assert.True(await validator.AcceptToMemoryPool(state, multiInputTx), $"Transaction: {nameof(multiInputTx)} failed mempool validation.");
        }

        /// <summary>
        /// Validate multi sig transactions in memory pool.
        /// Transaction scenario adapted from code project article referenced below.
        /// </summary>
        /// <seealso cref="https://www.codeproject.com/Articles/835098/NBitcoin-Build-Them-All"/>
        [Fact]
        public async Task AcceptToMemoryPool_WithMultiSigValidTxns_IsSuccessfulAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            var miner = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            ITestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, miner.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var alice = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var satoshi = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var nico = new BitcoinSecret(new Key(), KnownNetworks.RegTest);

            // corp needs two out of three of alice, bob, nico
            Script corpMultiSig = PayToMultiSigTemplate
                        .Instance
                        .GenerateScriptPubKey(2, new[] { alice.PubKey, bob.PubKey, nico.PubKey });

            // Fund corp
            // 50 Coins come from first tx on chain - send corp 42 and change back to miner
            var coin = new Coin(context.SrcTxs[0].GetHash(), 0, context.SrcTxs[0].TotalOut, miner.ScriptPubKey);
            var txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction sendToMultiSigTx = txBuilder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(miner)
                .Send(corpMultiSig, "42.00")
                .SendFees("0.001")
                .SetChange(miner.GetAddress())
                .BuildTransaction(true);
            Assert.True(txBuilder.Verify(sendToMultiSigTx)); //check fully signed
            var state = new MempoolValidationState(false);
            Assert.True(await validator.AcceptToMemoryPool(state, sendToMultiSigTx), $"Transaction: {nameof(sendToMultiSigTx)} failed mempool validation.");

            // AliceBobNico corp. send to Satoshi
            Coin[] corpCoins = sendToMultiSigTx.Outputs
                        .Where(o => o.ScriptPubKey == corpMultiSig)
                        .Select(o => new Coin(new OutPoint(sendToMultiSigTx.GetHash(), sendToMultiSigTx.Outputs.IndexOf(o)), o))
                        .ToArray();

            // Alice initiates the transaction
            txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction multiSigTx = txBuilder
                    .AddCoins(corpCoins)
                    .AddKeys(alice)
                    .Send(satoshi.GetAddress(), "4.5")
                    .SendFees("0.001")
                    .SetChange(corpMultiSig)
                    .BuildTransaction(true);
            Assert.True(!txBuilder.Verify(multiSigTx)); //Well, only one signature on the two required...

            // Nico completes the transaction
            txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            multiSigTx = txBuilder
                    .AddCoins(corpCoins)
                    .AddKeys(nico)
                    .SignTransaction(multiSigTx);
            Assert.True(txBuilder.Verify(multiSigTx));

            Assert.True(await validator.AcceptToMemoryPool(state, multiSigTx), $"Transaction: {nameof(multiSigTx)} failed mempool validation.");
        }

        /// <summary>
        /// Validate P2SH transaction in memory pool.
        /// Transaction scenario adapted from code project article referenced below.
        /// </summary>
        /// <seealso cref="https://www.codeproject.com/Articles/835098/NBitcoin-Build-Them-All"/>
        [Fact]
        public async Task AcceptToMemoryPool_WithP2SHValidTxns_IsSuccessfulAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            var miner = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            ITestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, miner.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var alice = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var satoshi = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var nico = new BitcoinSecret(new Key(), KnownNetworks.RegTest);

            // corp needs two out of three of alice, bob, nico
            Script corpMultiSig = PayToMultiSigTemplate
                        .Instance
                        .GenerateScriptPubKey(2, new[] { alice.PubKey, bob.PubKey, nico.PubKey });

            // P2SH address for corp multi-sig
            BitcoinScriptAddress corpRedeemAddress = corpMultiSig.GetScriptAddress(KnownNetworks.RegTest);

            // Fund corp
            // 50 Coins come from first tx on chain - send corp 42 and change back to miner
            var coin = new Coin(context.SrcTxs[0].GetHash(), 0, context.SrcTxs[0].TotalOut, miner.ScriptPubKey);
            var txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction fundP2shTx = txBuilder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(miner)
                .Send(corpRedeemAddress, "42.00")
                .SendFees("0.001")
                .SetChange(miner.GetAddress())
                .BuildTransaction(true);
            Assert.True(txBuilder.Verify(fundP2shTx)); //check fully signed
            var state = new MempoolValidationState(false);
            Assert.True(await validator.AcceptToMemoryPool(state, fundP2shTx), $"Transaction: {nameof(fundP2shTx)} failed mempool validation.");

            // AliceBobNico corp. send 20 to Satoshi
            Coin[] corpCoins = fundP2shTx.Outputs
                        .Where(o => o.ScriptPubKey == corpRedeemAddress.ScriptPubKey)
                        .Select(o => ScriptCoin.Create(KnownNetworks.RegTest, new OutPoint(fundP2shTx.GetHash(), fundP2shTx.Outputs.IndexOf(o)), o, corpMultiSig))
                        .ToArray();

            txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction p2shSpendTx = txBuilder
                    .AddCoins(corpCoins)
                    .AddKeys(alice, bob)
                    .Send(satoshi.GetAddress(), "20")
                    .SendFees("0.001")
                    .SetChange(corpRedeemAddress)
                    .BuildTransaction(true);
            Assert.True(txBuilder.Verify(p2shSpendTx));

            Assert.True(await validator.AcceptToMemoryPool(state, p2shSpendTx), $"Transaction: {nameof(p2shSpendTx)} failed mempool validation.");
        }

        /// <summary>
        /// Validate P2WPKH transaction in memory pool.
        /// </summary>
        [Fact]
        public async Task AcceptToMemoryPool_WithP2WPKHValidTxns_IsSuccessfulAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            var miner = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            ITestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, miner.PubKey.WitHash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);

            // Fund Bob
            // 50 Coins come from first tx on chain - send bob 42 and change back to miner
            var witnessCoin = new Coin(context.SrcTxs[0].GetHash(), 0, context.SrcTxs[0].TotalOut, miner.PubKey.WitHash.ScriptPubKey);
            var txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction p2wpkhTx = txBuilder
                .AddCoins(witnessCoin)
                .AddKeys(miner)
                .Send(bob, "42.00")
                .SendFees("0.001")
                .SetChange(miner)
                .BuildTransaction(true);
            Assert.True(txBuilder.Verify(p2wpkhTx)); //check fully signed
            var state = new MempoolValidationState(false);
            Assert.True(await validator.AcceptToMemoryPool(state, p2wpkhTx), $"Transaction: {nameof(p2wpkhTx)} failed mempool validation.");
        }

        /// <summary>
        /// Validate P2WSH transaction in memory pool.
        /// </summary>
        [Fact]
        public async Task AcceptToMemoryPool_WithP2WSHValidTxns_IsSuccessfulAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            var miner = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            ITestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, miner.PubKey.ScriptPubKey.WitHash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);

            // Fund Bob
            // 50 Coins come from first tx on chain - send bob 42 and change back to miner
            ScriptCoin witnessCoin = ScriptCoin.Create(KnownNetworks.RegTest, context.SrcTxs[0].GetHash(), 0, context.SrcTxs[0].TotalOut, miner.PubKey.ScriptPubKey.WitHash.ScriptPubKey, miner.PubKey.ScriptPubKey).AssertCoherent(KnownNetworks.RegTest);
            var txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction p2wshTx = txBuilder
                .AddCoins(witnessCoin)
                .AddKeys(miner)
                .Send(bob, "42.00")
                .SendFees("0.001")
                .SetChange(miner)
                .BuildTransaction(true);
            Assert.True(txBuilder.Verify(p2wshTx)); //check fully signed
            var state = new MempoolValidationState(false);
            Assert.True(await validator.AcceptToMemoryPool(state, p2wshTx), $"Transaction: {nameof(p2wshTx)} failed mempool validation.");
        }

        /// <summary>
        /// Validate SegWit transaction in memory pool.
        /// </summary>
        [Fact]
        public async Task AcceptToMemoryPool_WithSegWitValidTxns_IsSuccessfulAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            var miner = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            ITestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, miner.PubKey.ScriptPubKey.WitHash.ScriptPubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);

            // Fund Bob
            // 50 Coins come from first tx on chain - send bob 42 and change back to miner
            ScriptCoin witnessCoin = ScriptCoin.Create(KnownNetworks.RegTest, context.SrcTxs[0].GetHash(), 0, context.SrcTxs[0].TotalOut, miner.PubKey.ScriptPubKey.WitHash.ScriptPubKey.Hash.ScriptPubKey, miner.PubKey.ScriptPubKey);
            var txBuilder = new TransactionBuilder(KnownNetworks.RegTest);
            Transaction p2shOverp2wpkh = txBuilder
                .AddCoins(witnessCoin)
                .AddKeys(miner)
                .Send(bob, "42.00")
                .SendFees("0.001")
                .SetChange(miner)
                .BuildTransaction(true);
            Assert.True(txBuilder.Verify(p2shOverp2wpkh)); //check fully signed

            // remove witness data from tx
            Transaction noWitTx = p2shOverp2wpkh.WithOptions(TransactionOptions.None, KnownNetworks.RegTest.Consensus.ConsensusFactory);

            Assert.Equal(p2shOverp2wpkh.GetHash(), noWitTx.GetHash());
            Assert.True(noWitTx.GetSerializedSize() < p2shOverp2wpkh.GetSerializedSize());

            Assert.True(txBuilder.Verify(p2shOverp2wpkh)); //check fully signed
            var state = new MempoolValidationState(false);
            Assert.True(await validator.AcceptToMemoryPool(state, p2shOverp2wpkh), $"Transaction: {nameof(p2shOverp2wpkh)} failed mempool validation.");
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsCoinbase_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            var network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            // Create a transaction that looks like a coinbase.
            tx.AddInput(new TxIn(new OutPoint(new uint256(0), uint.MaxValue), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.CENT * 11), destSecret.PubKeyHash));
            Assert.True(tx.IsCoinBase);

            // It is not necessary to sign the transaction as we want the scriptSig left untouched and (2 < size < 100) bytes to pass the coinbase size check.
            var state = new MempoolValidationState(false);
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);

            // Tests PreMempoolChecks context.Transaction.IsCoinBase
            Assert.False(isSuccess, "Coinbase should not be accepted to mempool.");
            Assert.Equal(MempoolErrors.Coinbase, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsCoinbaseWithInvalidSize_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            var network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            // Create a transaction that looks like a coinbase. But the consensus rules that apply to coinbases will reject it anyway.
            // We need two forms of the test because submitting an incorrectly constructed coinbase triggers a consensus rule instead
            // of the coinbase being rejected by the mempool for being a coinbase.
            // TODO: Instead of creating multiple versions of all such tests we should perhaps find a way of testing the applicable rules in the mempool simultaneously.
            tx.AddInput(new TxIn(new OutPoint(new uint256(0), uint.MaxValue), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.CENT * 11), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);
            Assert.True(tx.IsCoinBase);

            var state = new MempoolValidationState(false);
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);

            // Tests PreMempoolChecks invokes CheckPowTransactionRule and is enforced for mempool transactions.
            Assert.False(isSuccess, "Coinbase with incorrect size should not be accepted to mempool.");
            Assert.Equal("bad-cb-length", state.Error.ConsensusError.Code);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsCoinstake_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            var network = KnownNetworks.StratisMain;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            Transaction tx = network.CreateTransaction();

            // Create a transaction that looks like a coinstake.
            // It is not a true PosTransaction coinstake, but this is sufficient to trigger the test condition.
            tx.AddInput(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            tx.AddOutput(new TxOut(Money.Zero, (IDestination)null));
            tx.AddOutput(new TxOut(Money.Zero, (IDestination)null));
            Assert.True(tx.IsCoinStake);

            var state = new MempoolValidationState(false);
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);

            // Tests PreMempoolChecks context.Transaction.IsCoinStake
            Assert.False(isSuccess, "Coinstake should not be accepted to mempool.");
            Assert.Equal(MempoolErrors.Coinstake, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsNonStandardVersionUnsupported_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.Satoshis(1)), destSecret.PubKeyHash));
            tx.Version = 0;
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the version number case in PreMempoolChecks CheckStandardTransaction (version too low)
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with version too low should not have been accepted.");
            Assert.Equal(MempoolErrors.Version, state.Error);

            // Can't reuse previous transaction with bumped version due to signing process having side effects.
            tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.Satoshis(1)), destSecret.PubKeyHash));
            tx.Version = (uint)validator.ConsensusOptions.MaxStandardVersion + 1;
            tx.Sign(network, minerSecret, false);

            state = new MempoolValidationState(false);

            // Tests the version number case in PreMempoolChecks CheckStandardTransaction (version too high)
            isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with version too high should not have been accepted.");
            Assert.Equal(MempoolErrors.Version, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsNonStandardTransactionSizeInvalid_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // Add outputs until transaction is large enough to fail standardness checks.
            while (MempoolValidator.GetTransactionWeight(tx, network.Consensus.Options) < this.Network.Consensus.Options.MaxStandardTxWeight)
            {
                tx.AddOutput(new TxOut(Money.Coins(1), minerSecret.PubKeyHash));
            }

            // It is not necessary to sign the transaction as we are testing a standardness
            // requirement which is evaluated before signatures in the mempool.

            var state = new MempoolValidationState(false);

            // Tests the transaction size standardness check in PreMempoolChecks CheckStandardTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction that is too large should not have been accepted.");
            Assert.Equal(MempoolErrors.TxSize, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsNonStandardInputScriptSigsLengthInvalid_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            Op checkSigOp = new Op() { Code = OpcodeType.OP_CHECKSIG };

            List<Op> sigOps = new List<Op>();

            // A scriptSig should not be longer than 10 000 bytes.
            for (int i = 0; i < 10100; i++)
            {
                sigOps.Add(checkSigOp);
            }

            var script = new Script(sigOps);

            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), script));
            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), destSecret.PubKeyHash));

            var state = new MempoolValidationState(false);

            // Tests the oversize scriptSig case in PreMempoolChecks CheckStandardTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with an oversize scriptSig should not have been accepted.");
            Assert.Equal(MempoolErrors.ScriptsigSize, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsNonStandardScriptSigIsPushOnly_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            List<Op> scriptSigOpcodes = new List<Op>();

            // Create a garbage scriptSig.
            for (int i = 0; i < 10; i++)
            {
                scriptSigOpcodes.Add(new Op() { Code = OpcodeType.OP_XOR });
            }

            var script = new Script(scriptSigOpcodes);

            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), script));
            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), destSecret.PubKeyHash));

            var state = new MempoolValidationState(false);

            // Tests the scriptSig only contains push operations case in PreMempoolChecks CheckStandardTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with non-push operations in the scriptSig should not have been accepted.");
            Assert.Equal(MempoolErrors.ScriptsigNotPushonly, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsNonStandardScriptTemplateIsNull_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), (IDestination)null));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the output script template null case in PreMempoolChecks CheckStandardTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with dust output should not have been accepted.");
            Assert.Equal(MempoolErrors.Scriptpubkey, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsNonStandardOutputIsDust_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.Satoshis(1)), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the dust output case in PreMempoolChecks CheckStandardTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with dust output should not have been accepted.");
            Assert.Equal(MempoolErrors.Dust, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxIsNonStandardOutputNotSingleReturn_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // Add two OP_RETURN (nulldata) outputs.
            tx.AddOutput(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 0, 0 })));
            tx.AddOutput(new TxOut(Money.Zero, TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 1, 1 })));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the multiple OP_RETURN output case in PreMempoolChecks CheckStandardTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with multiple OP_RETURN outputs should not have been accepted.");
            Assert.Equal(MempoolErrors.MultiOpReturn, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxFinalCannotMine_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // This is somewhat counterintuitive, but the nSequence of at least one input must
            // not be equal to SEQUENCE_FINAL in order for nLockTime functionality to be active.
            // SEQUENCE_LOCKTIME_DISABLE_FLAG disables the relative locktime.
            // See BIP68: https://github.com/bitcoin/bips/blob/master/bip-0068.mediawiki
            tx.Inputs.First().Sequence = new Sequence(Sequence.SEQUENCE_LOCKTIME_DISABLE_FLAG);

            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), destSecret.PubKeyHash));

            // Set the nLockTime to an arbitrary high block number to trigger the rejection logic.
            tx.LockTime = new LockTime(5000);

            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the final transaction case in PreMempoolChecks CheckFinalTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with nLockTime further than next block should not have been accepted.");
            Assert.Equal(MempoolErrors.NonFinal, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxAlreadyExists_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            var network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.CENT * 11), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.True(isSuccess, "Transaction should have been accepted but was not.");

            // This tests the method this.memPool.Exists(context.TransactionHash)
            isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction should not have been accepted a second time to the mempool.");
            Assert.Equal(MempoolErrors.InPool, state.Error);
        }

        [Fact(Skip = "It does not seem to be possible for this test case to separately test the AlreadyKnown error condition, as the equivalent InPool check precedes it in the validator.")]
        public async void AcceptToMemoryPool_TxAlreadyHaveCoins_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // By using the entire UTXO value the fee will be zero.
            tx.AddOutput(new TxOut(context.SrcTxs[0].Outputs[0].Value, destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Create dummy entry for the mempool.
            var entry = new TxMempoolEntry(tx, Money.Coins(1), 1, 0, 1, Money.Coins(1), true, 0, null, network.Consensus.Options);

            // Force the transaction into the TxMempool.
            ITxMempool txMempool = (ITxMempool)validator.GetMemberValue("memPool");
            txMempool.AddUnchecked(tx.GetHash(), entry);

            // Tests the case CheckMempoolCoinView context.View.HaveCoins(context.TransactionHash)
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction already in mempool should not have been accepted.");
            Assert.Equal(MempoolErrors.AlreadyKnown, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxMissingInputs_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            var network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            // Nonexistent input.
            tx.AddInput(new TxIn(new OutPoint(new uint256(57), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.CENT * 11), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // CheckMempoolCoinView !context.View.HaveCoins(txin.PrevOut.Hash)
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with invalid input should not have been accepted.");
            Assert.Equal("bad-txns-inputs-missing", state.Error.Code);
            Assert.Equal(16, state.Error.RejectCode);
            Assert.True(state.MissingInputs);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxInputsAreBad_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            // Need to use a non-mempool input otherwise it triggers the CheckConflicts logic instead.
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].Inputs.First().PrevOut.Hash, 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(Money.Coins(1), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the inputs missing case, CheckMempoolCoinView !context.View.HaveCoins(txin.PrevOut.Hash)
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with an input missing should not have been accepted.");
            Assert.True(state.MissingInputs);

            // TODO: Also need test for !context.View.HaveInputs(context.Transaction) case. It is not immediately obvious how to trigger the one failure but not the other. Maybe by messing with the spentness?
        }

        [Fact]
        public async void AcceptToMemoryPool_NonBIP68CanMine_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            // Need version >= 2 for BIP68 to be enforced.
            tx.Version = 2;

            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), destSecret.PubKeyHash));

            // Per BIP68 the nSequence value of an input can be used to restrict its spendability
            // prior to a certain timestamp or block height. Here we set it to an arbitrary high
            // value to trigger a BIP68 check failure.
            tx.Inputs.First().Sequence = new Sequence(5000);
            //tx.LockTime = new LockTime(0);
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the non-final BIP68 transaction case in CreateMempoolEntry CheckSequenceLocks
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with nSequence relative lock time further than next block should not have been accepted.");
            Assert.Equal(MempoolErrors.NonBIP68Final, state.Error);

            // TODO: Test for a time-based lock in addition to only a height-based one.
        }

        [Fact(Skip = "Not implemented yet.")]
        public void AcceptToMemoryPool_NonStandardP2SH_ReturnsFalse()
        {
            // TODO: Execute failure cases for CreateMempoolEntry AreInputsStandard
        }

        [Fact(Skip = "Not implemented yet.")]
        public void AcceptToMemoryPool_NonStandardP2WSH_ReturnsFalse()
        {
            // TODO: Execute failure cases for P2WSH Transactions CreateMempoolEntry
        }

        [Fact(Skip = "This is triggering the wrong error case. Also awaiting fix for issue #2470")]
        public async void AcceptToMemoryPool_TxExcessiveSigOps_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            Op checkSigOp = new Op() { Code = OpcodeType.OP_CHECKSIG };

            List<Op> sigOps = new List<Op>();

            for (int i = 0; i < (network.Consensus.Options.MaxBlockSigopsCost + 1); i++)
            {
                sigOps.Add(checkSigOp);
            }

            var script = new Script(sigOps);

            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), script));
            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), destSecret.PubKeyHash));

            var state = new MempoolValidationState(false);

            // Tests the failure case (too many sigops) in CheckSigOps
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with too many sigops should not have been accepted.");
            Assert.Equal(MempoolErrors.TooManySigops, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxFeeInvalidLessThanMin_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // By using the entire UTXO value the fee will be zero.
            tx.AddOutput(new TxOut(context.SrcTxs[0].Outputs[0].Value, destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Force a non-zero minimum fee, even though the mempool is empty and hasn't gathered fee data.
            ITxMempool txMempool = (ITxMempool)validator.GetMemberValue("memPool");
            txMempool.SetPrivateVariableValue<double>("rollingMinimumFeeRate", 10);

            // Tests the less than minimum fee case in CheckFee
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with fee too low should not have been accepted.");
            Assert.Equal(MempoolErrors.MinFeeNotMet, state.Error);
        }

        [Fact(Skip = "Implementation not finished, it is not triggering the correct code path.")]
        public async void AcceptToMemoryPool_TxFeeInvalidInsufficentPriorityForFree_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // By using the entire UTXO value the fee will be zero.
            tx.AddOutput(new TxOut(context.SrcTxs[0].Outputs[0].Value, destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            MempoolSettings memPoolSettings = (MempoolSettings)validator.GetMemberValue("mempoolSettings");
            memPoolSettings.RelayPriority = true;

            // Tests the less than minimum fee case in CheckFee
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with insufficient priority should not have been accepted.");
            Assert.Equal(MempoolErrors.InsufficientPriority, state.Error);

            // TODO: Execute failure case for CheckFee
            // - Insufficient priority for free transaction
        }

        [Fact]
        public async void AcceptToMemoryPool_TxFeeInvalidAbsurdlyHighFee_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(context.SrcTxs[0].Outputs[0].Value - Money.Cents(50), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Force a non-zero absurd fee, even though the mempool is empty and hasn't gathered fee data.
            state.AbsurdFee = Money.Cents(10);

            // Tests the absurdly high fee case in CheckFee
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with fee too high should not have been accepted.");
            Assert.Equal(MempoolErrors.AbsurdlyHighFee, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxTooManyAncestors_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(context.SrcTxs[0].Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Chain together a sufficient number of dependent transactions to trigger the ancestor limit.
            for (int i = 0; i < MempoolValidator.DefaultAncestorLimit; i++)
            {
                await validator.AcceptToMemoryPool(state, tx);

                Transaction newTx = network.CreateTransaction();
                newTx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
                newTx.AddOutput(new TxOut(tx.Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
                newTx.Sign(network, minerSecret, false);

                tx = newTx;
            }

            // Tests the too many ancestors failure case in CheckAncestors
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with too many ancestors should not have been accepted.");
            Assert.Equal(MempoolErrors.TooLongMempoolChain, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxAncestorsConflictSpend_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            var miner = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            ITestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, miner.PubKey.Hash.ScriptPubKey, dataDir).ConfigureAwait(false);
            IMempoolValidator validator = context.MempoolValidator;
            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var txBuilder = new TransactionBuilder(KnownNetworks.RegTest);

            //Create Coin from first tx on chain
            var coin = new Coin(context.SrcTxs[0].GetHash(), 0, context.SrcTxs[0].TotalOut, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(miner.PubKey));

            //Send 10 to Bob and return the rest as change to miner
            Transaction originalTx = txBuilder
               .AddCoins(coin)
               .AddKeys(miner)
               .Send(bob, "10.00")
               .SendFees("0.001")
               .SetChange(miner)
               .BuildTransaction(true);
            var state = new MempoolValidationState(false);

            //Mempool should accept it, there's nothing wrong
            Assert.True(await validator.AcceptToMemoryPool(state, originalTx).ConfigureAwait(false), $"Transaction: {nameof(originalTx)} failed mempool validation.");

            //Create second transaction spending the same coin
            Transaction conflictingTx = txBuilder
               .AddCoins(coin)
               .AddKeys(miner)
               .Send(bob, "10.00")
               .SendFees("0.001")
               .SetChange(miner)
               .BuildTransaction(true);

            //Mempool should reject the second transaction
            Assert.False(await validator.AcceptToMemoryPool(state, conflictingTx).ConfigureAwait(false), $"Transaction: {nameof(conflictingTx)} should have failed mempool validation.");

            Directory.Delete(dataDir, true);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxReplacementInsufficientFees_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            Transaction tx = network.CreateTransaction();

            // Put a regular valid transaction into the mempool.
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(Money.Coins(49), minerSecret.PubKeyHash));

            // We need the sequence of all the transactions to be lower than (Sequence.Final - 1) to avoid triggering the conflict mempool error.
            // Therefore just use 1 here and 2 for the actual replacing transaction.
            tx.Inputs.First().Sequence = 1;

            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            Assert.True(await validator.AcceptToMemoryPool(state, tx));

            var tx2 = new Transaction();

            // Put another valid transaction into the mempool.
            tx2.AddInput(new TxIn(new OutPoint(context.SrcTxs[1].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx2.AddOutput(new TxOut(Money.Coins(49), minerSecret.PubKeyHash));

            tx2.Inputs.First().Sequence = 1;

            tx2.Sign(network, minerSecret, false);

            Assert.True(await validator.AcceptToMemoryPool(state, tx2));

            var tx3 = new Transaction();

            // This transaction has a higher fee, but refers to the (unconfirmed) output of the first transaction, which is still in the pool.
            tx3.AddInput(new TxIn(new OutPoint(tx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            // It also has a conflict with the second transaction.
            tx3.AddInput(new TxIn(new OutPoint(context.SrcTxs[1].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // Ensure the replacement transaction has a lower fee than the transaction it is replacing.
            tx3.AddOutput(new TxOut(Money.Coins(99), minerSecret.PubKeyHash));

            tx3.Inputs.First().Sequence = tx.Inputs.First().Sequence + 1;

            tx3.Sign(network, minerSecret, false);

            // Tests the insufficient fee replacement failure case, CheckReplacement ReplacementAddsUnconfirmed InsufficientFees
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx3);
            Assert.False(isSuccess, "Transaction attempting replacement, that refers to an unconfirmed input, should not have been accepted.");
            Assert.Equal(MempoolErrors.InsufficientFee, state.Error);
        }

        [Fact(Skip = "This may need to be converted to an integration test as it seems to require mining to get it to work properly.")]
        public async void AcceptToMemoryPool_TxReplacementTooManyReplacements_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var state = new MempoolValidationState(false);

            // General approach: try to make tx1 that splits some inputs into 5 outputs
            // Then make 5 txes that split these outputs into a further 21 chained transactions each (avoid exceeding ancestor limit)
            // Then make a replacement transaction that tries to spend the same inputs that all these >100 transactions depend on

            // WIP portion:
            // -> It turns out this does not in fact work due to descendant transaction limits (25).
            // Therefore the first transation splitting out to 5 outputs actually needs to get mined so it is no longer in the pool.
            // Then each of the 5 outputs can be sent in a new transaction each and mined.
            // Then the 5 separate transactions will each have <= 25 descendants and <= 25 ancestors.
            // End WIP portion

            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(Money.Coins(1), minerSecret.PubKeyHash));
            tx.AddOutput(new TxOut(Money.Coins(1), minerSecret.PubKeyHash));
            tx.AddOutput(new TxOut(Money.Coins(1), minerSecret.PubKeyHash));
            tx.AddOutput(new TxOut(Money.Coins(1), minerSecret.PubKeyHash));
            tx.AddOutput(new TxOut(Money.Coins(1), minerSecret.PubKeyHash));
            tx.Inputs.First().Sequence = 1;
            tx.Sign(network, minerSecret, false);

            Assert.True(await validator.AcceptToMemoryPool(state, tx));

            // To trigger the conflict checks we need to have another transaction available that we spend an output from.
            var tx2 = new Transaction();
            tx2.AddInput(new TxIn(new OutPoint(context.SrcTxs[1].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx2.AddOutput(new TxOut(Money.Coins(49), minerSecret.PubKeyHash));
            tx2.Inputs.First().Sequence = 1;
            tx2.Sign(network, minerSecret, false);

            Assert.True(await validator.AcceptToMemoryPool(state, tx2));

            // Chain together a sufficient number of dependent transactions for each of the 5 initial outputs without triggering the ancestor limit.
            Transaction tempTx = null;

            tempTx = new Transaction();
            tempTx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tempTx.AddOutput(new TxOut(tx.Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
            tempTx.Inputs.First().Sequence = 1;
            tempTx.Sign(network, minerSecret, false);

            for (int i = 0; i < (MempoolValidator.DefaultAncestorLimit - 3); i++)
            {
                await validator.AcceptToMemoryPool(state, tempTx);

                var newTx = new Transaction();
                newTx.AddInput(new TxIn(new OutPoint(tempTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
                newTx.AddOutput(new TxOut(tempTx.Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
                newTx.Inputs.First().Sequence = 1;
                newTx.Sign(network, minerSecret, false);

                tempTx = newTx;
            }

            tempTx = new Transaction();
            tempTx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tempTx.AddOutput(new TxOut(tx.Outputs[1].Value - Money.Cents(1), minerSecret.PubKeyHash));
            tempTx.Inputs.First().Sequence = 1;
            tempTx.Sign(network, minerSecret, false);

            for (int i = 0; i < (MempoolValidator.DefaultAncestorLimit - 3); i++)
            {
                await validator.AcceptToMemoryPool(state, tempTx);

                var newTx = new Transaction();
                newTx.AddInput(new TxIn(new OutPoint(tempTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
                newTx.AddOutput(new TxOut(tempTx.Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
                newTx.Inputs.First().Sequence = 1;
                newTx.Sign(network, minerSecret, false);

                tempTx = newTx;
            }

            tempTx = new Transaction();
            tempTx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 2), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tempTx.AddOutput(new TxOut(tx.Outputs[2].Value - Money.Cents(1), minerSecret.PubKeyHash));
            tempTx.Inputs.First().Sequence = 1;
            tempTx.Sign(network, minerSecret, false);

            for (int i = 0; i < (MempoolValidator.DefaultAncestorLimit - 3); i++)
            {
                await validator.AcceptToMemoryPool(state, tempTx);

                var newTx = new Transaction();
                newTx.AddInput(new TxIn(new OutPoint(tempTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
                newTx.AddOutput(new TxOut(tempTx.Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
                newTx.Inputs.First().Sequence = 1;
                newTx.Sign(network, minerSecret, false);

                tempTx = newTx;
            }

            tempTx = new Transaction();
            tempTx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 3), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tempTx.AddOutput(new TxOut(tx.Outputs[3].Value - Money.Cents(1), minerSecret.PubKeyHash));
            tempTx.Inputs.First().Sequence = 1;
            tempTx.Sign(network, minerSecret, false);

            for (int i = 0; i < (MempoolValidator.DefaultAncestorLimit - 3); i++)
            {
                await validator.AcceptToMemoryPool(state, tempTx);

                var newTx = new Transaction();
                newTx.AddInput(new TxIn(new OutPoint(tempTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
                newTx.AddOutput(new TxOut(tempTx.Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
                newTx.Inputs.First().Sequence = 1;
                newTx.Sign(network, minerSecret, false);

                tempTx = newTx;
            }

            tempTx = new Transaction();
            tempTx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 4), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tempTx.AddOutput(new TxOut(tx.Outputs[4].Value - Money.Cents(1), minerSecret.PubKeyHash));
            tempTx.Inputs.First().Sequence = 1;
            tempTx.Sign(network, minerSecret, false);

            for (int i = 0; i < (MempoolValidator.DefaultAncestorLimit - 3); i++)
            {
                await validator.AcceptToMemoryPool(state, tempTx);

                var newTx = new Transaction();
                newTx.AddInput(new TxIn(new OutPoint(tempTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
                newTx.AddOutput(new TxOut(tempTx.Outputs[0].Value - Money.Cents(1), minerSecret.PubKeyHash));
                newTx.Inputs.First().Sequence = 1;
                newTx.Sign(network, minerSecret, false);

                tempTx = newTx;
            }

            // Now create a replacement transaction that uses the same inputs.
            var finalTx = new Transaction();
            finalTx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            finalTx.AddInput(new TxIn(new OutPoint(context.SrcTxs[1].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            finalTx.AddOutput(new TxOut(Money.Cents(20), minerSecret.PubKeyHash));
            finalTx.AddOutput(new TxOut(Money.Cents(20), minerSecret.PubKeyHash));
            finalTx.Inputs.First().Sequence = tx.Inputs.First().Sequence + 1;
            finalTx.Sign(network, minerSecret, false);

            // Tests the too many potential replacements failure case in CheckReplacement TooManyPotentialReplacements
            bool isSuccess = await validator.AcceptToMemoryPool(state, finalTx);
            Assert.False(isSuccess, "Transaction with too many potential replacements should not have been accepted.");
            Assert.Equal(MempoolErrors.TooManyPotentialReplacements, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_TxReplacementAddsUnconfirmed_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            Transaction tx = network.CreateTransaction();

            // Put a regular valid transaction into the mempool.
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(Money.Coins(49), minerSecret.PubKeyHash));

            // We need the sequence of all the transactions to be lower than (Sequence.Final - 1) to avoid triggering the conflict mempool error.
            // Therefore just use 1 here and 2 for the actual replacing transaction.
            tx.Inputs.First().Sequence = 1;

            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            Assert.True(await validator.AcceptToMemoryPool(state, tx));

            var tx2 = new Transaction();

            // Put another valid transaction into the mempool.
            tx2.AddInput(new TxIn(new OutPoint(context.SrcTxs[1].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx2.AddOutput(new TxOut(Money.Coins(49), minerSecret.PubKeyHash));

            tx2.Inputs.First().Sequence = 1;

            tx2.Sign(network, minerSecret, false);

            Assert.True(await validator.AcceptToMemoryPool(state, tx2));

            // To trigger replacement the replacement transaction needs to have a higher fee than the transaction it is replacing.
            // To trigger the specific fault for this test, the replacing transaction needs to refer to an unconfirmed input in the mempool.

            var tx3 = new Transaction();

            // This transaction has a higher fee, but refers to the (unconfirmed) output of the first transaction, which is still in the pool.
            tx3.AddInput(new TxIn(new OutPoint(tx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            // It also has a conflict with the second transaction.
            tx3.AddInput(new TxIn(new OutPoint(context.SrcTxs[1].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // The fee should be very much higher than the other transactions, as this transaction has effectively almost two entire block rewards as inputs.
            tx3.AddOutput(new TxOut(Money.Coins(1), minerSecret.PubKeyHash));

            tx3.Inputs.First().Sequence = tx.Inputs.First().Sequence + 1;

            tx3.Sign(network, minerSecret, false);

            // Tests the unconfirmed input replacement failure case, CheckReplacement ReplacementAddsUnconfirmed
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx3);
            Assert.False(isSuccess, "Transaction attempting replacement, that refers to an unconfirmed input, should not have been accepted.");
            Assert.Equal(MempoolErrors.ReplacementAddsUnconfirmed, state.Error);
        }

        [Fact(Skip = "Not implemented yet.")]
        public void AcceptToMemoryPool_TxPowConsensusCheckInputNegativeOrOverflow_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs PowCoinViewRule.CheckInputs BadTransactionInputValueOutOfRange
        }

        [Fact]
        public async void AcceptToMemoryPool_TxPowConsensusCheckInputBadTransactionInBelowOut_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));

            // Want output value > inputs.
            // There are actually two separate consensus errors that can occur, but the order they are checked means that only one should ever be seen.
            // The errors are: ConsensusErrors.BadTransactionInBelowOut and ConsensusErrors.BadTransactionNegativeFee
            tx.AddOutput(new TxOut(context.SrcTxs[0].Outputs.First().Value + Money.Coins(1), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            // Tests the in below out case in CheckAllInputs CheckInputs PowCoinViewRule.CheckInputs BadTransactionInBelowOut
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with Vout money > Vin money should not have been accepted.");
            Assert.Equal("bad-txns-in-belowout", state.Error.ConsensusError.Code);
        }

        [Fact(Skip = "Not clear how to test this without triggering In Below Out check instead.")]
        public async void AcceptToMemoryPool_TxPowConsensusCheckInputNegativeFee_ReturnsFalseAsync()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs PowCoinViewRule.CheckInputs NegativeFee
        }

        [Fact(Skip = "Making transactions with very large inputs/outputs pass validation is difficult, WIP.")]
        public async void AcceptToMemoryPool_TxPowConsensusCheckInputFeeOutOfRange_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(network.Consensus.MaxMoney - 1), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            // Idea: Force transaction with very large output into mempool. Then use it as an ancestor transaction
            // to make a transaction with a very large fee.

            var state = new MempoolValidationState(false);

            // Tests the dust output case in PreMempoolChecks CheckStandardTransaction
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction with fee out of range should not have been accepted.");
            Assert.Equal("bad-txns-vout-toolarge", state.Error.ConsensusError.Code);

            // TODO: Execute failure case for CheckAllInputs CheckInputs PowCoinViewRule.CheckInputs FeeOutOfRange
        }

        [Fact(Skip = "The VerifyScriptConsensus code path referred to does not seem to exist.")]
        public void AcceptToMemoryPool_TxVerifyStandardScriptConsensusFailure_ReturnsFalse()
        {
            // Only the CoinViewRule CheckInputs method is called from the mempoool validator's CheckInputs.
            // CoinViewRule.CheckInputs does not deal with script validation.
            // TODO: Execute failure case for CheckAllInputs CheckInputs VerifyScriptConsensus for ScriptVerify.Standard
        }

        [Fact(Skip = "Not implemented yet.")]
        public void AcceptToMemoryPool_TxContextVerifyStandardScriptFailure_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs ctx.VerifyScript for ScriptVerify.Standard
        }

        [Fact(Skip = "The VerifyScriptConsensus code path referred to does not seem to exist.")]
        public void AcceptToMemoryPool_TxVerifyP2SHScriptConsensusFailure_ReturnsFalse()
        {
            // Only the CoinViewRule CheckInputs method is called from the mempoool validator's CheckInputs.
            // CoinViewRule.CheckInputs does not deal with script validation.
            // TODO: Execute failure case for CheckAllInputs CheckInputs VerifyScriptConsensus for ScriptVerify.P2SH
        }

        [Fact]
        public async void AcceptToMemoryPool_TxContextVerifyP2SHScriptFailure_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            // Run mempool tests on mainnet so that RequireStandard flag is set in the mempool settings.
            Network network = KnownNetworks.Main;
            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();

            // Do horrible and not really sensible things to the scriptSig.
            // We do need this to nominally be a P2SH transaction to trigger the right kind of failure.
            var sig = new ECDSASignature(BigInteger.Zero, BigInteger.Zero);
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToScriptHashTemplate.Instance.GenerateScriptSig(network, new[] { sig }, minerSecret.ScriptPubKey)));
            tx.AddOutput(new TxOut(new Money(Money.Coins(1)), destSecret.PubKeyHash));

            var state = new MempoolValidationState(false);

            // Tests the verify P2SH output case in CheckAllInputs CheckInputs ctx.VerifyScript for ScriptVerify.P2SH
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction that fails P2SH validation should not have been accepted.");
            Assert.Equal(MempoolErrors.MandatoryScriptVerifyFlagFailed, state.Error);
        }

        [Fact]
        public async void AcceptToMemoryPool_MemPoolFull_ReturnsFalseAsync()
        {
            string dataDir = GetTestDirectoryPath(this);

            Network network = KnownNetworks.Main;

            var minerSecret = new BitcoinSecret(new Key(), network);
            ITestChainContext context = await TestChainFactory.CreateAsync(network, minerSecret.PubKey.Hash.ScriptPubKey, dataDir);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            var destSecret = new BitcoinSecret(new Key(), network);
            Transaction tx = network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.CENT * 11), destSecret.PubKeyHash));
            tx.Sign(network, minerSecret, false);

            var state = new MempoolValidationState(false);

            MempoolSettings memPoolSettings = (MempoolSettings)validator.GetMemberValue("mempoolSettings");
            memPoolSettings.MaxMempool = 0;

            // Test failure case for mempool size limit after pool is trimmed by LimitMempoolSize
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.False(isSuccess, "Transaction should not have been accepted when mempool is full.");
            Assert.Equal(MempoolErrors.Full, state.Error);
        }
    }
}
