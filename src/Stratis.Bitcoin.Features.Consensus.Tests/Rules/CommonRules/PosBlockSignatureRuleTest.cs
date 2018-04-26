using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosBlockSignatureRuleTest : TestPosConsensusRulesUnitTestBase
    {
        private Key key;

        public PosBlockSignatureRuleTest()
        {
            this.key = new Key();
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlockSignatureNotEmpty_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block()
            {
                BlockSignatur = new BlockSignature() { Signature = new byte[] { 0x2, 0x3 } }
            };
              
            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlockSignatureEmpty_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.BlockValidationContext.Block.Transactions.Add(transaction);

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_CoinStakePayToPubScriptKeyInvalid_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            // use different key with PayToPubKey script
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            Script scriptPubKeyOut = new Script(Op.GetPushOp(new Key().PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_NoOpsInScriptPubKey_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script()));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_FirstOpInScriptPubKeyNotOP_Return_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_CHECKSIG })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_OpCountBelowTwo_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_RETURN })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyDoesNotPassCompressedUncompresedKeyValidation_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_RETURN }, new Op() { PushData = new byte[] { 0x11 } })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyDoesNotPassBlockSignatureValidation_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            // push op_return to note external dependancy in front of pay to pubkey script so it does not match pay to pubkey template.
            // use a different key to generate the script so it does not pass validation.
            Script scriptPubKeyOut = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(new Key().PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyPassesBlockSignatureValidation_DoesNotThrowExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            // push op_return to note external dependancy in front of pay to pubkey script so it does not match pay to pubkey template.
            Script scriptPubKeyOut = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_PayToPubKeyScriptPassesBlockSignatureValidation_DoesNotThrowExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            Script scriptPubKeyOut = new Script(Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.BlockValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_BlockSignatureEmpty_DoesNotThrowExceptionAsync()
        {
            var block = new Block();
            block.Transactions.Add(new Transaction());

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(uint256.Zero, uint.MaxValue),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            block.Transactions.Add(transaction);
            block.BlockSignatur = new BlockSignature();

            this.ruleContext.BlockValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.BlockValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }
    }
}
