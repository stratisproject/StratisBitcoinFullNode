﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
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
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class APIFullNodeControllerTest : LogsTestBase
    {
        private ConcurrentChain chain;
        private readonly Mock<IFullNode> fullNode;
        private readonly Mock<IChainState> chainState;
        private readonly Mock<IConnectionManager> connectionManager;
        private readonly Network network;
        private NodeSettings nodeSettings;
        private readonly Mock<IPooledTransaction> pooledTransaction;
        private readonly Mock<IPooledGetUnspentTransaction> pooledGetUnspentTransaction;
        private readonly Mock<IGetUnspentTransaction> getUnspentTransaction;
        private readonly Mock<IConsensusLoop> consensusLoop;
        private readonly Mock<INetworkDifficulty> networkDifficulty;
        private APIFullNodeController controller;

        public APIFullNodeControllerTest()
        {
            this.fullNode = new Mock<IFullNode>();
            this.chainState = new Mock<IChainState>();
            this.connectionManager = new Mock<IConnectionManager>();
            this.network = Network.TestNet;
            this.connectionManager.Setup(c => c.Network).Returns(this.network);
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.nodeSettings = new NodeSettings();
            this.pooledTransaction = new Mock<IPooledTransaction>();
            this.pooledGetUnspentTransaction = new Mock<IPooledGetUnspentTransaction>();
            this.getUnspentTransaction = new Mock<IGetUnspentTransaction>();
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.networkDifficulty = new Mock<INetworkDifficulty>();
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                 this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
        }

        [Fact]
        public async Task Stop_WithoutFullNode_DoesNotThrowExceptionAsync()
        {
            IFullNode fullNode = null;
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, fullNode, this.nodeSettings);
            await this.controller.Stop().ConfigureAwait(false);
        }

        [Fact]
        public async Task Stop_WithFullNode_DisposesFullNodeAsync()
        {
            await this.controller.Stop().ConfigureAwait(false);
            this.fullNode.Verify(f => f.Dispose());
        }

        [Fact]
        public async Task GetRawTransactionAsync_BadTransactionID_ThrowsArgumentExceptionAsync()
        {
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = "abcd1234",
                verbose = false
            };
            
            IActionResult result = await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);
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
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString(),
                verbose = false
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);

            Assert.Null(json.Value);
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
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString(),
                verbose = false
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);
            TransactionBriefModel resultModel = (TransactionBriefModel)json.Value;

            Assert.NotNull(json);
            var model = Assert.IsType<TransactionBriefModel>(resultModel);
            Assert.Equal(transaction.ToHex(), model.Hex);
        }

        [Fact]
        public async Task GetRawTransactionAsync_NullVerbose_ReturnsBriefModelAsync()
        {
            uint256 txId = new uint256(12142124);
            Transaction transaction = this.CreateTransaction();
            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxAsync(txId))
                .ReturnsAsync(transaction);
            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString()
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);
            TransactionBriefModel resultModel = (TransactionBriefModel)json.Value;

            Assert.NotNull(resultModel);
            Assert.IsType<TransactionBriefModel>(resultModel);
            Assert.Equal(transaction.ToHex(), resultModel.Hex);
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
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString(),
                verbose = false
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);
            TransactionBriefModel resultModel = (TransactionBriefModel)json.Value;

            Assert.NotNull(resultModel);
            Assert.Equal(transaction.ToHex(), resultModel.Hex);
        }

        [Fact]
        public async Task GetRawTransactionAsync_PooledTransactionAndBlockStoreServiceNotAvailable_ReturnsNullAsync()
        {
            uint256 txId = new uint256(12142124);
            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(default(IBlockStore))
                .Verifiable();
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString(),
                verbose = false
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);

            Assert.Null(json.Value);
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
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString(),
                verbose = true
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);
            TransactionVerboseModel resultModel = (TransactionVerboseModel)json.Value;

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
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString(),
                verbose = true
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);
            TransactionVerboseModel resultModel = (TransactionVerboseModel)json.Value;

            Assert.NotNull(resultModel);
            var model = Assert.IsType<TransactionVerboseModel>(resultModel);
            Assert.Null(model.Confirmations);
        }

        [Fact]
        public async Task GetTaskAsync_Verbose_BlockNotFoundOnChain_ReturnsTransactionVerboseModelWithoutBlockInformationAsync()
        {
            Transaction transaction = this.CreateTransaction();
            uint256 txId = new uint256(12142124);
            this.pooledTransaction.Setup(p => p.GetTransaction(txId))
                .ReturnsAsync(transaction);
            var blockStore = new Mock<IBlockStore>();
            blockStore.Setup(b => b.GetTrxBlockIdAsync(txId))
                .ReturnsAsync((uint256)null);
            this.fullNode.Setup(f => f.NodeFeature<IBlockStore>(false))
                .Returns(blockStore.Object);
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, this.pooledTransaction.Object, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetRawTransactionRequestModel request = new GetRawTransactionRequestModel
            {
                txid = txId.ToString(),
                verbose = true
            };

            var json = (JsonResult)await this.controller.GetRawTransactionAsync(request).ConfigureAwait(false);
            TransactionVerboseModel resultModel = (TransactionVerboseModel)json.Value;

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

            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = "abcd1234",
                vout = "0",
                includeMemPool = false
            };

            IActionResult result = await this.controller.GetTxOutAsync(request).ConfigureAwait(false);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
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
            
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString()
            };

            var json = (JsonResult) await this.controller.GetTxOutAsync(request).ConfigureAwait(false);
            GetTxOutModel resultModel = (GetTxOutModel)json.Value;

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
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "0",
                includeMemPool = false
            };
            
            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(false);

            Assert.Null(json.Value);
            this.getUnspentTransaction.Verify();
        }

        [Fact]
        public async Task GetTxOutAsync_NotIncludeInMempool_GetUnspentTransactionNotAvailable_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "0",
                includeMemPool = false
            };

            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(false);

            Assert.Null(json.Value);
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeMempool_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.pooledGetUnspentTransaction.Setup(s => s.GetUnspentTransactionAsync(txId))
                .ReturnsAsync((UnspentOutputs)null)
                .Verifiable();
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "0",
                includeMemPool = true
            };

            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(true);

            Assert.Null(json.Value);
            this.pooledGetUnspentTransaction.Verify();
        }

        [Fact]
        public async Task GetTxOutAsync_IncludeMempool_PooledGetUnspentTransactionNotAvailable_UnspentTransactionNotFound_ReturnsNullAsync()
        {
            var txId = new uint256(1243124);
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "0",
                includeMemPool = true
            };

            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(false);

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
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "0",
                includeMemPool = false
            };

            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(false);
            GetTxOutModel resultModel = (GetTxOutModel)json.Value;

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
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "0",
                includeMemPool = true
            };

            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(false);
            GetTxOutModel resultModel = (GetTxOutModel)json.Value;

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
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "13",
                includeMemPool = false
            };

            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(false);
            GetTxOutModel resultModel = (GetTxOutModel)json.Value;

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
            GetTxOutRequestModel request = new GetTxOutRequestModel
            {
                txid = txId.ToString(),
                vout = "13",
                includeMemPool = true
            };

            var json = (JsonResult)await this.controller.GetTxOutAsync(request).ConfigureAwait(false);
            GetTxOutModel resultModel = (GetTxOutModel)json.Value;

            this.pooledGetUnspentTransaction.Verify();
            Assert.Equal(this.chain.Tip.HashBlock, resultModel.BestBlock);
            Assert.True(resultModel.Coinbase);
            Assert.Equal(3, resultModel.Confirmations);
            Assert.Null(resultModel.ScriptPubKey);
            Assert.Null(resultModel.Value);
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

            var json = (JsonResult)this.controller.GetBlockCount();
            int result = int.Parse(json.Value.ToString());

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

            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.Equal((uint)14999899, resultModel.Version);
            Assert.Equal((uint)ProtocolVersion.NO_BLOOM_VERSION, resultModel.ProtocolVersion);
            Assert.Equal(3, resultModel.Blocks);
            Assert.Equal(0, resultModel.TimeOffset);
            Assert.Equal(0, resultModel.Connections);
            Assert.Empty(resultModel.Proxy);
            Assert.Equal(new Target(121221121212).Difficulty, resultModel.Difficulty);
            Assert.True(resultModel.Testnet);
            Assert.Equal(0.00001m, resultModel.RelayFee);
            Assert.Empty(resultModel.Errors);
            Assert.Null(resultModel.WalletVersion);
            Assert.Null(resultModel.Balance);
            Assert.Null(resultModel.KeypoolOldest);
            Assert.Null(resultModel.KeypoolSize);
            Assert.Null(resultModel.UnlockedUntil);
            Assert.Null(resultModel.PayTxFee);
        }

        [Fact]
        public void GetInfo_MainNet_ReturnsInfoModel()
        {
            Network network = Network.Main;
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);

            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.False(resultModel.Testnet);
        }

        [Fact]
        public void GetInfo_NoChainState_ReturnsModel()
        {
            IChainState chainState = null;
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, chainState, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);

            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.Equal(0, resultModel.Blocks);
        }

        [Fact]
        public void GetInfo_NoChainTip_ReturnsModel()
        {
            this.chainState.Setup(c => c.ConsensusTip)
                .Returns((ChainedHeader)null);
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);

            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.Equal(0, resultModel.Blocks);
        }

        [Fact]
        public void GetInfo_NoSettings_ReturnsModel()
        {
            this.nodeSettings = null;
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.Equal((uint)NodeSettings.SupportedProtocolVersion, resultModel.ProtocolVersion);
            Assert.Equal(0, resultModel.RelayFee);
        }

        [Fact]
        public void GetInfo_NoConnectionManager_ReturnsModel()
        {
            IConnectionManager connectionManager = null;

            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, connectionManager, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);
            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.Equal(0, resultModel.TimeOffset);
            Assert.Null(resultModel.Connections);
        }

        [Fact]
        public void GetInfo_NoNetworkDifficulty_ReturnsModel()
        {
            INetworkDifficulty networkDifficulty = null;
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, networkDifficulty,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);

            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.Equal(0, resultModel.Difficulty);
        }

        [Fact]
        public void GetInfo_NoVersion_ReturnsModel()
        {
            this.fullNode.Setup(f => f.Version)
              .Returns((Version)null);
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);

            var json = (JsonResult)this.controller.GetInfo();
            GetInfoModel resultModel = (GetInfoModel)json.Value;

            Assert.Equal((uint)0, resultModel.Version);
        }

        [Fact]
        public void GetBlockHeader_NotUsingJsonFormat_ThrowsNotImplementedException()
        {
            GetBlockHeaderRequestModel request = new GetBlockHeaderRequestModel
            {
                hash = "1341323442",
                isJsonFormat = false
            };

            IActionResult result = this.controller.GetBlockHeader(request);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.NotImplementedException", error.Description);
        }

        [Fact]
        public void GetBlockHeader_ChainNull_ReturnsNull()
        {
            this.chain = null;
            GetBlockHeaderRequestModel request = new GetBlockHeaderRequestModel
            {
                hash = "12341341545245",
                isJsonFormat = true
            };
            this.controller = new APIFullNodeController(this.LoggerFactory.Object, this.network, this.chain, this.chainState.Object, this.connectionManager.Object, null, this.pooledGetUnspentTransaction.Object, this.getUnspentTransaction.Object, this.networkDifficulty.Object,
                 this.consensusLoop.Object, this.fullNode.Object, this.nodeSettings);

            var json = (JsonResult)this.controller.GetBlockHeader(request);

            BlockHeaderModel resultModel = (BlockHeaderModel)json.Value;
            Assert.Null(resultModel);
        }

        [Fact]
        public void GetBlockHeader_BlockHeaderFound_ReturnsBlockHeaderModel()
        {
            var block = this.chain.GetBlock(2);
            var bits = GetBlockHeaderBits(block.Header);
            GetBlockHeaderRequestModel request = new GetBlockHeaderRequestModel
            {
                hash = block.HashBlock.ToString(),
                isJsonFormat = true
            };

            var json = (JsonResult)this.controller.GetBlockHeader(request);
            BlockHeaderModel resultModel = (BlockHeaderModel)json.Value;

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
            GetBlockHeaderRequestModel request = new GetBlockHeaderRequestModel
            {
                hash = new uint256(2562).ToString(),
                isJsonFormat = true
            };

            var json = (JsonResult)this.controller.GetBlockHeader(request);
            BlockHeaderModel resultModel = (BlockHeaderModel)json.Value;

            Assert.Null(resultModel);
        }

        [Fact]
        public void ValidateAddress_IsNotAValidBase58Address_ThrowsFormatException()
        {
            ValidateAddressRequestModel request = new ValidateAddressRequestModel
            {
                address = "invalidaddress"
            };

            IActionResult result = this.controller.ValidateAddress(request);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void ValidateAddress_ValidAddressOfDifferentNetwork_ReturnsFalse()
        {
            // P2PKH
            var address = new Key().PubKey.GetAddress(Network.Main);
            ValidateAddressRequestModel request = new ValidateAddressRequestModel
            {
                address = address.ToString()
            };

            var json = (JsonResult)this.controller.ValidateAddress(request);
            ValidatedAddress resultModel = (ValidatedAddress)json.Value;

            var isValid = resultModel.IsValid;
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2PKHAddress_ReturnsTrue()
        {
            // P2PKH
            var address = new Key().PubKey.GetAddress(this.network);
            ValidateAddressRequestModel request = new ValidateAddressRequestModel
            {
                address = address.ToString()
            };

            var json = (JsonResult)this.controller.ValidateAddress(request);
            ValidatedAddress resultModel = (ValidatedAddress)json.Value;

            var isValid = resultModel.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2SHAddress_ReturnsTrue()
        {
            // P2SH
            var address = new Key().ScriptPubKey.GetScriptAddress(this.network);
            ValidateAddressRequestModel request = new ValidateAddressRequestModel
            {
                address = address.ToString()
            };

            var json = (JsonResult)this.controller.ValidateAddress(request);
            ValidatedAddress resultModel = (ValidatedAddress)json.Value;

            var isValid = resultModel.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2WPKHAddress_ReturnsTrue()
        {
            // P2WPKH
            var address = new Key().PubKey.WitHash.GetAddress(this.network);
            ValidateAddressRequestModel request = new ValidateAddressRequestModel
            {
                address = address.ToString()
            };

            var json = (JsonResult)this.controller.ValidateAddress(request);
            ValidatedAddress resultModel = (ValidatedAddress)json.Value;

            var isValid = resultModel.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateAddress_ValidP2WSHAddress_ReturnsTrue()
        {
            // P2WSH
            var address = new Key().PubKey.ScriptPubKey.WitHash.ScriptPubKey.GetWitScriptAddress(this.network);
            ValidateAddressRequestModel request = new ValidateAddressRequestModel
            {
                address = address.ToString()
            };

            var json = (JsonResult)this.controller.ValidateAddress(request);
            ValidatedAddress resultModel = (ValidatedAddress)json.Value;

            var isValid = resultModel.IsValid;
            Assert.True(isValid);
        }

        [Fact]
        public void AddNode_InvalidCommand_ThrowsArgumentException()
        {
            AddNodeRequestModel request = new AddNodeRequestModel
            {
                str_endpoint = "0.0.0.0",
                command = "notarealcommand"
            };

            IActionResult result = this.controller.AddNode(request);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);
        }

        [Fact]
        public void AddNode_InvalidEndpoint_ThrowsFormatException()
        {
            AddNodeRequestModel request = new AddNodeRequestModel
            {
                str_endpoint = "a.b.c.d",
                command = "onetry"
            };

            IActionResult result = this.controller.AddNode(request);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void AddNode_ValidCommand_ReturnsTrue()
        {
            AddNodeRequestModel request = new AddNodeRequestModel
            {
                str_endpoint = "0.0.0.0",
                command = "remove"
            };

            var json = (JsonResult)this.controller.AddNode(request);

            Assert.True((bool)json.Value);
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