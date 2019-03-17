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
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class AllowedScriptTypesRuleTest
    {
        private readonly Network network;

        private readonly AllowedScriptTypeRule rule;

        public AllowedScriptTypesRuleTest()
        {
            this.network = new SmartContractsRegTest();
            this.rule = new AllowedScriptTypeRule(this.network);
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

        [Fact]
        public void Actual_Withdrawal_Passes()
        {
            Transaction withdrawal = new SmartContractsPoATest().CreateTransaction("01000000019eb9793f7c69bd31d4b03518f349e70384f8f20456556b4be0f941dbaebec9d400000000fdfe0000483045022100f6d3d20ebfe9b336a1432e06365c549a3dac5b411cb0fe2cd6dd46b09530adf202206e65df4c8f18f66f65409652463300c8b1b90ad69486c9ebabd58987baeeb7fd01483045022100e399eec964ccc1d99b1a0b285031284e7c4a4b0aa678dd52ea4698973c326622022011814dddddcd4a43fd4a223509de4815e2d687814c5fc5abde68ec3801026fe1014c69522102eef7619de25578c9717a289d08c61d4598b2bd81d2ee5db3072a07fa2d121e6521027ce19209dd1212a6a4fc2b7ddf678c6dea40b596457f934f73f3dcc5d0d9ee552103093239d5344ddb4c69c46c75bd629519e0b68d2cfc1a86cd63115fd068f202ba53aeffffffff03c0dc8743fd1a070017a91442938bb61378468a38629c4ffa1521759d0283578700e1f505000000001976a9148732134e7953ebfe51f65d455612a4245f9610ae88ac0000000000000000226a2009422af22360465d208f70e4c86284e538706db5db5dae1c2c4fcad5eef928eb00000000");

            rule.CheckTransaction(new MempoolValidationContext(withdrawal, null));
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
