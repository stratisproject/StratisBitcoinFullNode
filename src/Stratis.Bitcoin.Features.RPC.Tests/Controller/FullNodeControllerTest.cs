using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class FullNodeControllerTest : LogsTestBase
    {
        private ConcurrentChain chain;
        private readonly Mock<IFullNode> fullNode;
        private readonly Mock<IChainState> chainState;
        private readonly Mock<IConnectionManager> connectionManager;
        private Network network;
        private NodeSettings nodeSettings;
        private readonly Mock<IPooledTransaction> pooledTransaction;
        private readonly Mock<IPooledGetUnspentTransaction> pooledGetUnspentTransaction;
        private readonly Mock<IGetUnspentTransaction> getUnspentTransaction;
        private readonly Mock<IConsensusLoop> consensusLoop;
        private readonly Mock<INetworkDifficulty> networkDifficulty;
        private FullNodeController controller;
        private readonly bool initialBlockSignature;

        public FullNodeControllerTest()
        {
            this.fullNode = new Mock<IFullNode>();
            this.chainState = new Mock<IChainState>();
            this.connectionManager = new Mock<IConnectionManager>();
            this.network = Network.TestNet;
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.nodeSettings = new NodeSettings();
            this.pooledTransaction = new Mock<IPooledTransaction>();
            this.pooledGetUnspentTransaction = new Mock<IPooledGetUnspentTransaction>();
            this.getUnspentTransaction = new Mock<IGetUnspentTransaction>();
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.networkDifficulty = new Mock<INetworkDifficulty>();
            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
        }

        [Fact]
        public async Task Stop_WithoutFullNode_DoesNotThrowExceptionAsync()
        {
            IFullNode fullNode = null;

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, fullNode, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);

            await this.controller.Stop().ConfigureAwait(false);
        }

        [Fact]
        public async Task Stop_WithFullNode_DisposesFullNodeAsync()
        {
            await this.controller.Stop().ConfigureAwait(false);

            this.fullNode.Verify(f => f.Dispose());
        }

        [Fact]
        public async Task GetRawTransactionAsync_TransactionCannotBeFound_ReturnsNullAsync()
        {
            uint256 txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync((Transaction)null)
                .Verifiable();

            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxAsync(txId))
                .ReturnsAsync((Transaction)null)
                .Verifiable();

            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);

            var result = await this.controller.GetRawTransactionAsync(txId.ToString(), 0).ConfigureAwait(false);

            Assert.Null(result);
            this.pooledTransaction.Verify();
            blockStore.Verify();
        }

        [Fact]
        public async Task GetRawTransactionAsync_TransactionNotInPooledTransaction_ReturnsTransactionFromBlockStoreAsync()
        {
            uint256 txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync((Transaction)null);

            Transaction transaction = this.CreateTransaction();
            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxAsync(txId))
                .ReturnsAsync(transaction);

            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);

            var result = await this.controller.GetRawTransactionAsync(txId.ToString(), 0).ConfigureAwait(false);

            Assert.NotNull(result);
            var model = Assert.IsType<TransactionBriefModel>(result);
            Assert.Equal(transaction.ToHex(), model.Hex);
        }

        [Fact]
        public async Task GetRawTransactionAsync_PooledTransactionServiceNotAvailable_ReturnsTransactionFromBlockStoreAsync()
        {
            uint256 txId = new uint256(12142124);

            Transaction transaction = this.CreateTransaction();
            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxAsync(txId))
                .ReturnsAsync(transaction);

            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);

            var result = await this.controller.GetRawTransactionAsync(txId.ToString(), 0).ConfigureAwait(false);

            Assert.NotNull(result);
            var model = Assert.IsType<TransactionBriefModel>(result);
            Assert.Equal(transaction.ToHex(), model.Hex);
        }

        [Fact]
        public async Task GetRawTransactionAsync_PooledTransactionAndBlockStoreServiceNotAvailable_ReturnsNullAsync()
        {
            uint256 txId = new uint256(12142124);

            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(default(IBlockStore))
                .Verifiable();

            this.controller = new FullNodeController(this.LoggerFactory.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var result = await this.controller.GetRawTransactionAsync(txId.ToString(), 0).ConfigureAwait(false);

            Assert.Null(result);
            this.fullNode.Verify();
        }

        [Fact]
        public async Task GetTaskAsync_Verbose_ReturnsTransactionVerboseModelAsync()
        {
            this.chainState.Setup(c => c.ConsensusTip)
                .Returns(this.chain.Tip);
            var block = this.chain.GetBlock(1);

            Transaction transaction = this.CreateTransaction();
            uint256 txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync(transaction);

            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxBlockIdAsync(txId))
                .ReturnsAsync(block.HashBlock);

            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);

            var result = await this.controller.GetRawTransactionAsync(txId.ToString(), 1).ConfigureAwait(false);

            Assert.NotNull(result);
            var model = Assert.IsType<TransactionVerboseModel>(result);
            Assert.Equal(transaction.GetHash().ToString(), model.TxId);
            Assert.Equal(transaction.GetSerializedSize(), model.Size);
            Assert.Equal(transaction.Version, model.Version);
            Assert.Equal((uint)transaction.LockTime, model.LockTime);
            Assert.Equal(transaction.ToHex(), model.Hex);

            Assert.Equal(block.HashBlock.ToString(), model.BlockHash);
            Assert.Equal(3, model.Confirmations);
            Assert.Equal(Utils.DateTimeToUnixTime(block.Header.BlockTime), model.Time);
            Assert.Equal(Utils.DateTimeToUnixTime(block.Header.BlockTime), model.BlockTime);

            Assert.NotEmpty(model.VIn);
            var input = model.VIn[0];
            var expectedInput = new Vin(transaction.Inputs[0].PrevOut, transaction.Inputs[0].Sequence, transaction.Inputs[0].ScriptSig);
            Assert.Equal(expectedInput.Coinbase, input.Coinbase);
            Assert.Equal(expectedInput.ScriptSig, input.ScriptSig);
            Assert.Equal(expectedInput.Sequence, input.Sequence);
            Assert.Equal(expectedInput.TxId, input.TxId);
            Assert.Equal(expectedInput.VOut, input.VOut);

            Assert.NotEmpty(model.VOut);
            var output = model.VOut[0];
            var expectedOutput = new Vout(0, transaction.Outputs[0], this.network);
            Assert.Equal(expectedOutput.Value, output.Value);
            Assert.Equal(expectedOutput.N, output.N);
            Assert.Equal(expectedOutput.ScriptPubKey.Hex, output.ScriptPubKey.Hex);
        }

        [Fact]
        public async Task GetTaskAsync_Verbose_ChainStateTipNull_DoesNotCalulateConfirmationsAsync()
        {
            var block = this.chain.GetBlock(1);
            Transaction transaction = this.CreateTransaction();
            uint256 txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync(transaction);

            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxBlockIdAsync(txId))
                .ReturnsAsync(block.HashBlock);

            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);

            var result = await this.controller.GetRawTransactionAsync(txId.ToString(), 1).ConfigureAwait(false);

            Assert.NotNull(result);
            var model = Assert.IsType<TransactionVerboseModel>(result);
            Assert.Null(model.Confirmations);
        }

        [Fact]
        public async Task GetTaskAsync_Verbose_BlockNotFoundOnChain_ReturnsTransactionVerboseModelWithoutBlockInformationAsync()
        {
            var block = this.chain.GetBlock(1);
            Transaction transaction = this.CreateTransaction();
            uint256 txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync(transaction);

            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxBlockIdAsync(txId))
                .ReturnsAsync((uint256)null);

            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);

            var result = await this.controller.GetRawTransactionAsync(txId.ToString(), 1).ConfigureAwait(false);

            Assert.NotNull(result);
            var model = Assert.IsType<TransactionVerboseModel>(result);
            Assert.Null(model.BlockHash);
            Assert.Null(model.Confirmations);
            Assert.Null(model.Time);
            Assert.Null(model.BlockTime);
        }

        [Fact]
        public async Task GetTxOutAsync_NotIncludeInMempool_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.getUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync((UnspentOutputs)null)
                .Verifiable();

            var result = await this.controller.GetTxOutAsync(txId.ToString(), 0, false).ConfigureAwait(false);

            Assert.Null(result);
            this.getUnspentTransaction.Verify();
        }

        [Fact]
        public async Task GetTxOutAsync_NotIncludeInMempool_GetUnspentTransactionNotAvailable_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, null, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var result = await this.controller.GetTxOutAsync(txId.ToString(), 0, false).ConfigureAwait(false);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeMempool_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.pooledGetUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync((UnspentOutputs)null)
                .Verifiable();

            var result = await this.controller.GetTxOutAsync(txId.ToString(), 0, true).ConfigureAwait(true);

            Assert.Null(result);
            this.pooledGetUnspentTransaction.Verify();
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeMempool_PooledGetUnspentTransactionNotAvailable_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, null, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var result = await this.controller.GetTxOutAsync(txId.ToString(), 0, true).ConfigureAwait(false);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTxOutAsync_NotIncludeInMempool_UnspentTransactionFound_ReturnsModelAsync()
        {
            var txId = new uint256(1243124);
            Transaction transaction = this.CreateTransaction();
            var unspentOutputs = new UnspentOutputs(1, transaction);

            this.getUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync(unspentOutputs)
                .Verifiable();

            var model = await this.controller.GetTxOutAsync(txId.ToString(), 0, false).ConfigureAwait(false);

            this.getUnspentTransaction.Verify();

            Assert.Equal(this.chain.Tip.HashBlock, model.BestBlock);
            Assert.True(model.Coinbase);
            Assert.Equal(3, model.Confirmations);
            Assert.Equal(new ScriptPubKey(transaction.Outputs[0].ScriptPubKey, this.network).Hex, model.ScriptPubKey.Hex);
            Assert.Equal(transaction.Outputs[0].Value, model.Value);
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeInMempool_UnspentTransactionFound_ReturnsModelAsync()
        {
            var txId = new uint256(1243124);
            Transaction transaction = this.CreateTransaction();
            var unspentOutputs = new UnspentOutputs(1, transaction);

            this.pooledGetUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync(unspentOutputs)
                .Verifiable();

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var model = await this.controller.GetTxOutAsync(txId.ToString(), 0, true).ConfigureAwait(false);

            this.pooledGetUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, model.BestBlock);
            Assert.True(model.Coinbase);
            Assert.Equal(3, model.Confirmations);
            Assert.Equal(new ScriptPubKey(transaction.Outputs[0].ScriptPubKey, this.network).Hex, model.ScriptPubKey.Hex);
            Assert.Equal(transaction.Outputs[0].Value, model.Value);
        }

        [Fact]
        public async Task GetTxOutAsync_NotIncludeInMempool_UnspentTransactionFound_VOutNotFound_ReturnsModelAsync()
        {
            var txId = new uint256(1243124);
            Transaction transaction = this.CreateTransaction();
            var unspentOutputs = new UnspentOutputs(1, transaction);

            this.getUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync(unspentOutputs)
                .Verifiable();

            var model = await this.controller.GetTxOutAsync(txId.ToString(), 13, false).ConfigureAwait(false);

            this.getUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, model.BestBlock);
            Assert.True(model.Coinbase);
            Assert.Equal(3, model.Confirmations);
            Assert.Null(model.ScriptPubKey);
            Assert.Null(model.Value);
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeInMempool_UnspentTransactionFound_VOutNotFound_ReturnsModelAsync()
        {
            var txId = new uint256(1243124);
            Transaction transaction = this.CreateTransaction();
            var unspentOutputs = new UnspentOutputs(1, transaction);

            this.pooledGetUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync(unspentOutputs)
                .Verifiable();

            var model = await this.controller.GetTxOutAsync(txId.ToString(), 13, true).ConfigureAwait(false);

            this.pooledGetUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, model.BestBlock);
            Assert.True(model.Coinbase);
            Assert.Equal(3, model.Confirmations);
            Assert.Null(model.ScriptPubKey);
            Assert.Null(model.Value);
        }

        [Fact]
        public void GetBlockCount_ReturnsHeightFromConsensusLoopTip()
        {
            this.consensusLoop.Setup(c => c.Tip)
                .Returns(this.chain.GetBlock(2));

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(s => s.GetService(typeof(IConsensusLoop)))
                .Returns(this.consensusLoop.Object);

            this.fullNode.Setup(f => f.Services.ServiceProvider)
                .Returns(serviceProvider.Object);

            var result = this.controller.GetBlockCount();

            Assert.Equal(2, result);
        }

        [Fact]
        public void GetInfo_TestNet_ReturnsInfoModel()
        {
            this.fullNode.Setup(f => f.Version)
                .Returns(new Version(15, 0));
            this.networkDifficulty.Setup(n => n.GetNetworkDifficulty())
                .Returns(new Target(121221121212));

            this.nodeSettings.ProtocolVersion = ProtocolVersion.NO_BLOOM_VERSION;
            this.nodeSettings.MinRelayTxFeeRate = new FeeRate(new Money(1000));

            this.chainState.Setup(c => c.ConsensusTip)
                .Returns(this.chain.Tip);

            this.connectionManager.Setup(c => c.ConnectedPeers)
                .Returns(new TestReadOnlyNetworkPeerCollection());

            var model = this.controller.GetInfo();

            Assert.Equal((uint)14999899, model.Version);
            Assert.Equal((uint)ProtocolVersion.NO_BLOOM_VERSION, model.ProtocolVersion);
            Assert.Equal(3, model.Blocks);
            Assert.Equal(0, model.TimeOffset);
            Assert.Equal(0, model.Connections);
            Assert.Empty(model.Proxy);
            Assert.Equal(new Target(121221121212).Difficulty, model.Difficulty);
            Assert.True(model.Testnet);
            Assert.Equal(0.00001m, model.RelayFee);
            Assert.Empty(model.Errors);

            Assert.Null(model.WalletVersion);
            Assert.Null(model.Balance);
            Assert.Null(model.KeypoolOldest);
            Assert.Null(model.KeypoolSize);
            Assert.Null(model.UnlockedUntil);
            Assert.Null(model.PayTxFee);
        }

        [Fact]
        public void GetInfo_MainNet_ReturnsInfoModel()
        {
            this.network = Network.Main;

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var model = this.controller.GetInfo();

            Assert.False(model.Testnet);
        }

        [Fact]
        public void GetInfo_NoChainState_ReturnsModel()
        {
            IChainState chainState = null;

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, chainState, this.connectionManager.Object);
            var model = this.controller.GetInfo();

            Assert.Equal(0, model.Blocks);
        }

        [Fact]
        public void GetInfo_NoChainTip_ReturnsModel()
        {
            this.chainState.Setup(c => c.ConsensusTip)
                .Returns((ChainedBlock)null);

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var model = this.controller.GetInfo();

            Assert.Equal(0, model.Blocks);
        }

        [Fact]
        public void GetInfo_NoSettings_ReturnsModel()
        {
            this.nodeSettings = null;
            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var model = this.controller.GetInfo();

            Assert.Equal((uint)NodeSettings.SupportedProtocolVersion, model.ProtocolVersion);
            Assert.Equal(0, model.RelayFee);
        }

        [Fact]
        public void GetInfo_NoConnectionManager_ReturnsModel()
        {
            IConnectionManager connectionManager = null;

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, connectionManager);
            var model = this.controller.GetInfo();

            Assert.Equal(0, model.TimeOffset);
            Assert.Null(model.Connections);
        }

        [Fact]
        public void GetInfo_NoNetworkDifficulty_ReturnsModel()
        {
            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, null,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var model = this.controller.GetInfo();

            Assert.Equal(0, model.Difficulty);
        }

        [Fact]
        public void GetInfo_NoVersion_ReturnsModel()
        {
            this.fullNode.Setup(f => f.Version)
              .Returns((Version)null);

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var model = this.controller.GetInfo();

            Assert.Equal((uint)0, model.Version);
        }

        [Fact]
        public void GetBlockHeader_NotUsingJsonFormat_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() =>
            {
                this.controller.GetBlockHeader("", false);
            });
        }

        [Fact]
        public void GetBlockHeader_ChainNull_ReturnsNull()
        {
            this.chain = null;

            this.controller = new FullNodeController(this.LoggerFactory.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings, this.network, this.chain, this.chainState.Object, this.connectionManager.Object);
            var result = this.controller.GetBlockHeader("", true);

            Assert.Null(result);
        }


        [Fact]
        public void GetBlockHeader_BlockHeaderFound_ReturnsBlockHeaderModel()
        {
            var block = this.chain.GetBlock(2);
            var bits = GetBlockHeaderBits(block.Header);

            var result = this.controller.GetBlockHeader(block.HashBlock.ToString(), true);

            Assert.NotNull(result);
            Assert.Equal((uint)block.Header.Version, result.Version);
            Assert.Equal(block.Header.HashPrevBlock.ToString(), result.PreviousBlockHash);
            Assert.Equal(block.Header.HashMerkleRoot.ToString(), result.MerkleRoot);
            Assert.Equal(block.Header.Time, result.Time);
            Assert.Equal((int)block.Header.Nonce, result.Nonce);
            Assert.Equal(bits, result.Bits);
        }

        [Fact]
        public void GetBlockHeader_BlockHeaderNotFound_ReturnsNull()
        {
            var result = this.controller.GetBlockHeader(new uint256(2562).ToString(), true);

            Assert.Null(result);
        }

        [Fact]
        public void ValidateAddress_IsNotAValidBase58Address_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() =>
            {
                this.controller.ValidateAddress("invalidaddress");
            });
        }

        [Fact]
        public void ValidateAddress_ValidAddressOfDifferentNetwork_ReturnsFalse()
        {
            // P2PKH
            var address = new Key().PubKey.GetAddress(Network.Main);

            var result = this.controller.ValidateAddress(address.ToString());

            var isValid = result.IsValid;
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2PKHAddress_ReturnsTrue()
        {
            // P2PKH
            var address = new Key().PubKey.GetAddress(this.network);

            var result = this.controller.ValidateAddress(address.ToString());

            var isValid = result.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2SHAddress_ReturnsTrue()
        {
            // P2SH
            var address = new Key().ScriptPubKey.GetScriptAddress(this.network);

            var result = this.controller.ValidateAddress(address.ToString());

            var isValid = result.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2WPKHAddress_ReturnsTrue()
        {
            // P2WPKH
            var address = new Key().PubKey.WitHash.GetAddress(this.network);

            var result = this.controller.ValidateAddress(address.ToString());

            var isValid = result.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2WSHAddress_ReturnsTrue()
        {
            // P2WSH
            var address = new Key().PubKey.ScriptPubKey.WitHash.ScriptPubKey.GetWitScriptAddress(this.network);

            var result = this.controller.ValidateAddress(address.ToString());

            var isValid = result.IsValid;
            Assert.True(isValid);
        }

        private Transaction CreateTransaction()
        {
            var transaction = new Transaction();
            transaction.AddInput(TxIn.CreateCoinbase(23523523));
            transaction.AddOutput(new TxOut(this.network.GetReward(23523523), new Key().ScriptPubKey));
            return transaction;
        }

        private string GetBlockHeaderBits(BlockHeader header)
        {

            byte[] bytes = this.GetBytes(header.Bits.ToCompact());
            return Encoders.Hex.EncodeData(bytes);
        }

        private byte[] GetBytes(uint compact)
        {
            return new byte[]
            {
                (byte)(compact >> 24),
                (byte)(compact >> 16),
                (byte)(compact >> 8),
                (byte)(compact)
            };
        }

        public class TestReadOnlyNetworkPeerCollection : IReadOnlyNetworkPeerCollection
        {
            public event EventHandler<NetworkPeerEventArgs> Added;
            public event EventHandler<NetworkPeerEventArgs> Removed;

            private List<INetworkPeer> networkPeers;

            public TestReadOnlyNetworkPeerCollection()
            {
                this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
                this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
                this.networkPeers = new List<INetworkPeer>();
            }

            public TestReadOnlyNetworkPeerCollection(List<INetworkPeer> peers)
            {
                this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
                this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
                this.networkPeers = peers;
            }

            public INetworkPeer FindByEndpoint(IPEndPoint endpoint)
            {
                return null;
            }

            public INetworkPeer FindByIp(IPAddress ip)
            {
                return null;
            }

            public INetworkPeer FindLocal()
            {
                return null;
            }

            public IEnumerator<INetworkPeer> GetEnumerator()
            {
                return this.networkPeers.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}