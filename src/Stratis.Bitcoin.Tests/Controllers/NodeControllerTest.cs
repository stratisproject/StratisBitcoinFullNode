using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Tests.Controllers
{
    public class NodeControllerTest : LogsTestBase
    {
        private readonly ConcurrentChain chain;
        private readonly Mock<IChainState> chainState;
        private readonly Mock<IConnectionManager> connectionManager;
        private readonly Mock<IDateTimeProvider> dateTimeProvider;
        private readonly Mock<IFullNode> fullNode;
        private readonly Mock<IPeerBanning> peerBanning;
        private readonly Network network;
        private readonly NodeSettings nodeSettings;

        private readonly Mock<IBlockStore> blockStore;
        private readonly Mock<IGetUnspentTransaction> getUnspentTransaction;
        private readonly Mock<INetworkDifficulty> networkDifficulty;
        private readonly Mock<IPooledGetUnspentTransaction> pooledGetUnspentTransaction;
        private readonly Mock<IPooledTransaction> pooledTransaction;

        private NodeController controller;

        public NodeControllerTest()
        {
            this.network = KnownNetworks.TestNet;

            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.chainState = new Mock<IChainState>();
            this.connectionManager = new Mock<IConnectionManager>();
            this.connectionManager.Setup(c => c.Network).Returns(this.network);
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.fullNode = new Mock<IFullNode>();
            this.nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin);
            this.peerBanning = new Mock<IPeerBanning>();

            this.blockStore = new Mock<IBlockStore>();
            this.getUnspentTransaction = new Mock<IGetUnspentTransaction>();
            this.networkDifficulty = new Mock<INetworkDifficulty>();
            this.pooledGetUnspentTransaction = new Mock<IPooledGetUnspentTransaction>();
            this.pooledTransaction = new Mock<IPooledTransaction>();

            this.controller = new NodeController(
                this.chain,
                this.chainState.Object,
                this.connectionManager.Object,
                this.dateTimeProvider.Object,
                this.fullNode.Object,
                this.LoggerFactory.Object,
                this.nodeSettings,
                this.network,
                this.blockStore.Object,
                this.getUnspentTransaction.Object,
                this.networkDifficulty.Object,
                this.pooledGetUnspentTransaction.Object,
                this.pooledTransaction.Object);
        }

        [Fact]
        public void Stop_WithFullNode_DisposesFullNode()
        {
            var isDisposed = false;
            this.fullNode.Setup(f => f.NodeLifetime.StopApplication()).Callback(() => isDisposed = true);

            IActionResult result = this.controller.Shutdown(true);

            result.Should().BeOfType<OkResult>();
            Thread.Sleep(100);
            isDisposed.Should().BeTrue();
        }

        [Fact]
        public async Task GetRawTransactionAsync_BadTransactionID_ThrowsArgumentExceptionAsync()
        {
            string txid = "abcd1234";
            bool verbose = false;

            IActionResult result = await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);
        }

        [Fact]
        public async Task GetRawTransactionAsync_TransactionCannotBeFound_ReturnsNullAsync()
        {
            var txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync((Transaction)null)
                .Verifiable();
            this.blockStore.Setup(b => b.GetTransactionByIdAsync(txId))
                .ReturnsAsync((Transaction)null)
                .Verifiable();
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);

            string txid = txId.ToString();
            bool verbose = false;

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);

            Assert.Null(json.Value);
            this.pooledTransaction.Verify();
            this.blockStore.Verify();
        }

        [Fact]
        public async Task GetRawTransactionAsync_TransactionNotInPooledTransaction_ReturnsTransactionFromBlockStoreAsync()
        {
            var txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync((Transaction)null);
            Transaction transaction = this.CreateTransaction();
            this.blockStore.Setup(b => b.GetTransactionByIdAsync(txId))
                .ReturnsAsync(transaction);
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            bool verbose = false;

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);
            var resultModel = (TransactionBriefModel)json.Value;

            Assert.NotNull(json);
            var model = Assert.IsType<TransactionBriefModel>(resultModel);
            Assert.Equal(transaction.ToHex(), model.Hex);
        }

        [Fact]
        public async Task GetRawTransactionAsync_NullVerbose_ReturnsBriefModelAsync()
        {
            var txId = new uint256(12142124);
            Transaction transaction = this.CreateTransaction();
            this.blockStore.Setup(b => b.GetTransactionByIdAsync(txId))
                .ReturnsAsync(transaction);
            string txid = txId.ToString();

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid).ConfigureAwait(false);
            var resultModel = (TransactionBriefModel)json.Value;

            Assert.NotNull(resultModel);
            Assert.IsType<TransactionBriefModel>(resultModel);
            Assert.Equal(transaction.ToHex(), resultModel.Hex);
        }

        [Fact]
        public async Task GetRawTransactionAsync_PooledTransactionServiceNotAvailable_ReturnsTransactionFromBlockStoreAsync()
        {
            var txId = new uint256(12142124);
            Transaction transaction = this.CreateTransaction();
            this.blockStore.Setup(b => b.GetTransactionByIdAsync(txId))
                .ReturnsAsync(transaction);
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            bool verbose = false;

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);
            var resultModel = (TransactionBriefModel)json.Value;

            Assert.NotNull(resultModel);
            Assert.Equal(transaction.ToHex(), resultModel.Hex);
        }

        [Fact]
        public async Task GetRawTransactionAsync_PooledTransactionAndBlockStoreServiceNotAvailable_ReturnsNullAsync()
        {
            var txId = new uint256(12142124);
            this.blockStore.Setup(f => f.GetTransactionByIdAsync(txId))
                .ReturnsAsync((Transaction)null)
                .Verifiable();
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            bool verbose = false;

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);

            Assert.Null(json.Value);
            this.fullNode.Verify();
        }

        [Fact]
        public void DecodeRawTransaction_ReturnsTransaction()
        {
            var txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync((Transaction)null);
            Transaction transaction = this.CreateTransaction();

            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);

            var json = (JsonResult)this.controller.DecodeRawTransaction(new DecodeRawTransactionModel() { RawHex = transaction.ToHex() });
            var resultModel = (TransactionVerboseModel)json.Value;

            Assert.NotNull(json);
            var model = Assert.IsType<TransactionVerboseModel>(resultModel);
            Assert.Equal(transaction.ToHex(), model.Hex);
        }

        [Fact]
        public async Task GetTaskAsync_Verbose_ReturnsTransactionVerboseModelAsync()
        {
            this.chainState.Setup(c => c.ConsensusTip)
                .Returns(this.chain.Tip);
            ChainedHeader block = this.chain.GetBlock(1);
            Transaction transaction = this.CreateTransaction();
            var txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync(transaction);
            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetBlockIdByTransactionIdAsync(txId))
                .ReturnsAsync(block.HashBlock);
            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);
            string txid = txId.ToString();
            bool verbose = true;

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);
            var resultModel = (TransactionVerboseModel)json.Value;

            Assert.NotNull(resultModel);
            var model = Assert.IsType<TransactionVerboseModel>(resultModel);
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
            Vin input = model.VIn[0];
            var expectedInput = new Vin(transaction.Inputs[0].PrevOut, transaction.Inputs[0].Sequence, transaction.Inputs[0].ScriptSig);
            Assert.Equal(expectedInput.Coinbase, input.Coinbase);
            Assert.Equal(expectedInput.ScriptSig, input.ScriptSig);
            Assert.Equal(expectedInput.Sequence, input.Sequence);
            Assert.Equal(expectedInput.TxId, input.TxId);
            Assert.Equal(expectedInput.VOut, input.VOut);
            Assert.NotEmpty(model.VOut);
            Vout output = model.VOut[0];
            var expectedOutput = new Vout(0, transaction.Outputs[0], this.network);
            Assert.Equal(expectedOutput.Value, output.Value);
            Assert.Equal(expectedOutput.N, output.N);
            Assert.Equal(expectedOutput.ScriptPubKey.Hex, output.ScriptPubKey.Hex);
        }

        [Fact]
        public async Task GetTaskAsync_Verbose_ChainStateTipNull_DoesNotCalulateConfirmationsAsync()
        {
            ChainedHeader block = this.chain.GetBlock(1);
            Transaction transaction = this.CreateTransaction();
            var txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync(transaction);

            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetBlockIdByTransactionIdAsync(txId))
                .ReturnsAsync(block.HashBlock);
            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);
            string txid = txId.ToString();
            bool verbose = true;

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);
            var resultModel = (TransactionVerboseModel)json.Value;

            Assert.NotNull(resultModel);
            var model = Assert.IsType<TransactionVerboseModel>(resultModel);
            Assert.Null(model.Confirmations);
        }

        [Fact]
        public async Task GetTaskAsync_Verbose_BlockNotFoundOnChain_ReturnsTransactionVerboseModelWithoutBlockInformationAsync()
        {
            Transaction transaction = this.CreateTransaction();
            var txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync(transaction);
            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetBlockIdByTransactionIdAsync(txId))
                .ReturnsAsync((uint256)null);
            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);
            string txid = txId.ToString();
            bool verbose = true;

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(txid, verbose).ConfigureAwait(false);
            var resultModel = (TransactionVerboseModel)json.Value;

            Assert.NotNull(resultModel);
            var model = Assert.IsType<TransactionVerboseModel>(resultModel);
            Assert.Null(model.BlockHash);
            Assert.Null(model.Confirmations);
            Assert.Null(model.Time);
            Assert.Null(model.BlockTime);
        }

        [Fact]
        public async Task GetTxOutAsync_InvalidTxID_ThrowsArgumentExceptionAsync()
        {
            string txid = "abcd1234";
            uint vout = 0;
            bool includeMemPool = false;

            IActionResult result = await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);
        }

        [Fact]
        public async Task GetTxOutAsync_NullVoutandInMempool_PooledUnspentTransactionFound_ReturnsModelVoutZeroAsync()
        {
            var txId = new uint256(1243124);
            Transaction transaction = this.CreateTransaction();
            var unspentOutputs = new UnspentOutputs(1, transaction);
            this.pooledGetUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync(unspentOutputs)
                .Verifiable();
            string txid = txId.ToString();

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid).ConfigureAwait(false);
            var resultModel = (GetTxOutModel)json.Value;

            this.getUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, resultModel.BestBlock);
            Assert.True(resultModel.Coinbase);
            Assert.Equal(3, resultModel.Confirmations);
            Assert.Equal(new ScriptPubKey(transaction.Outputs[0].ScriptPubKey, this.network).Hex, resultModel.ScriptPubKey.Hex);
            Assert.Equal(transaction.Outputs[0].Value, resultModel.Value);
        }

        [Fact]
        public async Task GetTxOutAsync_NotIncludeInMempool_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.getUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync((UnspentOutputs)null)
                .Verifiable();
            string txid = txId.ToString();
            uint vout = 0;
            bool includeMemPool = false;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);

            Assert.Null(json.Value);
            this.getUnspentTransaction.Verify();
        }

        [Fact]
        public async Task GetTxOutAsync_NotIncludeInMempool_GetUnspentTransactionNotAvailable_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            uint vout = 0;
            bool includeMemPool = false;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);

            Assert.Null(json.Value);
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeMempool_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.pooledGetUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync((UnspentOutputs)null)
                .Verifiable();
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            uint vout = 0;
            bool includeMemPool = true;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(true);

            Assert.Null(json.Value);
            this.pooledGetUnspentTransaction.Verify();
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeMempool_PooledGetUnspentTransactionNotAvailable_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            uint vout = 0;
            bool includeMemPool = true;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);

            Assert.Null(json.Value);
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
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            uint vout = 0;
            bool includeMemPool = false;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);
            var resultModel = (GetTxOutModel)json.Value;

            this.getUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, resultModel.BestBlock);
            Assert.True(resultModel.Coinbase);
            Assert.Equal(3, resultModel.Confirmations);
            Assert.Equal(new ScriptPubKey(transaction.Outputs[0].ScriptPubKey, this.network).Hex, resultModel.ScriptPubKey.Hex);
            Assert.Equal(transaction.Outputs[0].Value, resultModel.Value);
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
            this.controller = new NodeController(this.chain, this.chainState.Object,
                this.connectionManager.Object, this.dateTimeProvider.Object, this.fullNode.Object,
                this.LoggerFactory.Object, this.nodeSettings, this.network, this.blockStore.Object, this.getUnspentTransaction.Object,
                this.networkDifficulty.Object, this.pooledGetUnspentTransaction.Object, this.pooledTransaction.Object);
            string txid = txId.ToString();
            uint vout = 0;
            bool includeMemPool = true;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);
            var resultModel = (GetTxOutModel)json.Value;

            this.pooledGetUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, resultModel.BestBlock);
            Assert.True(resultModel.Coinbase);
            Assert.Equal(3, resultModel.Confirmations);
            Assert.Equal(new ScriptPubKey(transaction.Outputs[0].ScriptPubKey, this.network).Hex, resultModel.ScriptPubKey.Hex);
            Assert.Equal(transaction.Outputs[0].Value, resultModel.Value);
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
            string txid = txId.ToString();
            uint vout = 13;
            bool includeMemPool = false;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);
            var resultModel = (GetTxOutModel)json.Value;

            this.getUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, resultModel.BestBlock);
            Assert.True(resultModel.Coinbase);
            Assert.Equal(3, resultModel.Confirmations);
            Assert.Null(resultModel.ScriptPubKey);
            Assert.Null(resultModel.Value);
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
            string txid = txId.ToString();
            uint vout = 13;
            bool includeMemPool = true;

            var json = (JsonResult)await this.controller.GetTxOutAsync(txid, vout, includeMemPool).ConfigureAwait(false);
            var resultModel = (GetTxOutModel)json.Value;

            this.pooledGetUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, resultModel.BestBlock);
            Assert.True(resultModel.Coinbase);
            Assert.Equal(3, resultModel.Confirmations);
            Assert.Null(resultModel.ScriptPubKey);
            Assert.Null(resultModel.Value);
        }

        [Fact]
        public void GetBlockHeader_NotUsingJsonFormat_ThrowsNotImplementedException()
        {
            string hash = "1341323442";
            bool isJsonFormat = false;

            IActionResult result = this.controller.GetBlockHeader(hash, isJsonFormat);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.NotImplementedException", error.Description);
        }

        [Fact]
        public void GetBlockHeader_BlockHeaderFound_ReturnsBlockHeaderModel()
        {
            ChainedHeader block = this.chain.GetBlock(2);
            string bits = GetBlockHeaderBits(block.Header);
            string hash = block.HashBlock.ToString();
            bool isJsonFormat = true;

            var json = (JsonResult)this.controller.GetBlockHeader(hash, isJsonFormat);
            var resultModel = (BlockHeaderModel)json.Value;

            Assert.NotNull(resultModel);
            Assert.Equal((uint)block.Header.Version, resultModel.Version);
            Assert.Equal(block.Header.HashPrevBlock.ToString(), resultModel.PreviousBlockHash);
            Assert.Equal(block.Header.HashMerkleRoot.ToString(), resultModel.MerkleRoot);
            Assert.Equal(block.Header.Time, resultModel.Time);
            Assert.Equal((int)block.Header.Nonce, resultModel.Nonce);
            Assert.Equal(bits, resultModel.Bits);
        }

        [Fact]
        public void GetBlockHeader_BlockHeaderNotFound_ReturnsNull()
        {
            string hash = new uint256(2562).ToString();
            bool isJsonFormat = true;

            var json = (JsonResult)this.controller.GetBlockHeader(hash, isJsonFormat);
            var resultModel = (BlockHeaderModel)json.Value;

            Assert.Null(resultModel);
        }

        [Fact]
        public void ValidateAddress_IsNotAValidBase58Address_ThrowsFormatException()
        {
            string address = "invalidaddress";

            IActionResult result = this.controller.ValidateAddress(address);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void ValidateAddress_ValidAddressOfDifferentNetwork_ReturnsFalse()
        {
            // P2PKH
            BitcoinPubKeyAddress pubkeyaddress = new Key().PubKey.GetAddress(KnownNetworks.Main);
            string address = pubkeyaddress.ToString();

            var json = (JsonResult)this.controller.ValidateAddress(address);
            var resultModel = (ValidatedAddress)json.Value;

            bool isValid = resultModel.IsValid;
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2PKHAddress_ReturnsTrue()
        {
            // P2PKH
            BitcoinPubKeyAddress pubkeyaddress = new Key().PubKey.GetAddress(this.network);
            string address = pubkeyaddress.ToString();

            var json = (JsonResult)this.controller.ValidateAddress(address);
            var resultModel = (ValidatedAddress)json.Value;

            bool isValid = resultModel.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2SHAddress_ReturnsTrue()
        {
            // P2SH
            BitcoinScriptAddress scriptaddress = new Key().ScriptPubKey.GetScriptAddress(this.network);
            string address = scriptaddress.ToString();

            var json = (JsonResult)this.controller.ValidateAddress(address);
            var resultModel = (ValidatedAddress)json.Value;

            bool isValid = resultModel.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2WPKHAddress_ReturnsTrue()
        {
            // P2WPKH
            BitcoinAddress btcaddress = new Key().PubKey.WitHash.GetAddress(this.network);
            string address = btcaddress.ToString();

            var json = (JsonResult)this.controller.ValidateAddress(address);
            var resultModel = (ValidatedAddress)json.Value;

            bool isValid = resultModel.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2WSHAddress_ReturnsTrue()
        {
            // P2WSH
            BitcoinWitScriptAddress scriptaddress = new Key().PubKey.ScriptPubKey.WitHash.ScriptPubKey.GetWitScriptAddress(this.network);
            string address = scriptaddress.ToString();

            var json = (JsonResult)this.controller.ValidateAddress(address);
            var resultModel = (ValidatedAddress)json.Value;

            bool isValid = resultModel.IsValid;
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

            public List<INetworkPeer> FindByIp(IPAddress ip)
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