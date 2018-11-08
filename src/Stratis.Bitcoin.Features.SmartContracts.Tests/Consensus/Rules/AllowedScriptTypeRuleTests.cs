using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class AllowedScriptTypesRuleTest
    {
        private readonly Network network;

        private readonly TestContractRulesEngine rulesEngine;

        private readonly AllowedScriptTypeRule rule;

        public AllowedScriptTypesRuleTest()
        {
            this.network = new SmartContractsRegTest();

            var loggerFactory = new Mock<ILoggerFactory>();
            var dateTimeProvider = new Mock<IDateTimeProvider>();
            var chain = new Mock<ConcurrentChain>();
            var nodeDeployments = new Mock<NodeDeployments>();
            var consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network));
            var checkpoints = new Mock<ICheckpoints>();
            var coinView = new Mock<ICoinView>();
            var chainState = new Mock<ChainState>();
            var invalidBlockHashStore = new Mock<IInvalidBlockHashStore>();

            this.rulesEngine = new TestContractRulesEngine(this.network,
                loggerFactory.Object,
                dateTimeProvider.Object,
                chain.Object,
                new NodeDeployments(this.network, chain.Object),
                consensusSettings,
                checkpoints.Object,
                coinView.Object,
                chainState.Object,
                invalidBlockHashStore.Object,
                new NodeStats(new DateTimeProvider())
            );

            this.rule = new AllowedScriptTypeRule
            {
                Parent = this.rulesEngine
            };
            rule.Initialize();
        }

        [Fact]
        public void P2PKHInput_SmartContractCallOutput_Passes()
        {
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(GetP2PKHInput());
            transaction.Outputs.Add(new TxOut(100, new Script(new byte[] { (byte)ScOpcodeType.OP_CALLCONTRACT, 1, 2, 3 })));

            // No exception when checking
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        [Fact]
        public void P2PKHInput_SmartContractCreateOutput_Passes()
        {
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(GetP2PKHInput());
            transaction.Outputs.Add(new TxOut(100, new Script(new byte[] { (byte)ScOpcodeType.OP_CREATECONTRACT, 1, 2, 3 })));

            // No exception when checking
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        [Fact]
        public void P2PKHInput_P2PKHOutput_Passes()
        {
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(GetP2PKHInput());
            transaction.Outputs.Add(new TxOut(100, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId())));

            // No exception when checking
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        [Fact]
        public void OpSpendInput_OpInternalTransferOutput_Passes()
        {
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn(new Script(new byte[] { (byte)ScOpcodeType.OP_SPEND })));
            transaction.Outputs.Add(new TxOut(100, new Script(new byte[] { (byte)ScOpcodeType.OP_INTERNALCONTRACTTRANSFER })));

            // No exception when checking
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        [Fact]
        public void P2PKHInput_OpReturnOutput_Fails()
        {
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(GetP2PKHInput());
            transaction.Outputs.Add(new TxOut(100, new Script(new Op[] { OpcodeType.OP_RETURN })));

            Assert.ThrowsAny<Exception>(() => rule.CheckTransaction(new MempoolValidationContext(transaction, null)));
        }

        [Fact]
        public void MultiSigInput_P2PKHOutput_Passes()
        {
            // This occurs when receiving funds federation on our sidechain

            Transaction transaction = this.network.CreateTransaction();
            Script scriptSig = PayToMultiSigTemplate.Instance.GenerateScriptSig(new TransactionSignature[] {new Key().Sign(new uint256(0), SigHash.All), new Key().Sign(new uint256(0), SigHash.All)});
            transaction.Inputs.Add(new TxIn(scriptSig));
            transaction.Outputs.Add(new TxOut(100, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId())));

            // No exception when checking
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        [Fact]
        public void P2PKHInput_MultiSigOutput_Passes()
        {
            // This occurs when sending funds to the federation on our sidechain

            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(GetP2PKHInput());
            Script script = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, new PubKey[] {new Key().PubKey, new Key().PubKey, new Key().PubKey});
            transaction.Outputs.Add(new TxOut(100, script));

            // No exception when checking
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        private static TxIn GetP2PKHInput()
        {
            // Taken from MinerTests
            byte[] hex = Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f");

            var p2pkhParams = new PayToPubkeyHashScriptSigParameters
            {
                PublicKey = new PubKey(hex),
                TransactionSignature = TransactionSignature.Empty
            };

            var txIn = new TxIn(PayToPubkeyHashTemplate.Instance.GenerateScriptSig(p2pkhParams));
            txIn.PrevOut = new OutPoint();
            txIn.PrevOut.Hash = uint256.One; // so that PrevOut.NotNull is true, and thus the transaction won't be counted as a Coinbase.
            return txIn;
        }
    }
}
