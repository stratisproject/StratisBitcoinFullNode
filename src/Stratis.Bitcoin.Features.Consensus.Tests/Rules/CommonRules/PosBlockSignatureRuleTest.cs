using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
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
            this.ruleContext.ValidationContext.Block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();

            (this.ruleContext.ValidationContext.Block as PosBlock).BlockSignature = new BlockSignature() {Signature = new byte[] {0x2, 0x3}};
              
            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlockSignatureEmpty_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext.Block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.Block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            this.ruleContext.ValidationContext.Block.Transactions.Add(transaction);

            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_CoinStakePayToPubScriptKeyInvalid_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            // use different key with PayToPubKey script
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            var scriptPubKeyOut = new Script(Op.GetPushOp(new Key().PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_NoOpsInScriptPubKey_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script()));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_FirstOpInScriptPubKeyNotOP_Return_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_CHECKSIG })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_OpCountBelowTwo_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_RETURN })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyDoesNotPassCompressedUncompresedKeyValidation_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(new Op() { Code = OpcodeType.OP_RETURN }, new Op() { PushData = new byte[] { 0x11 } })));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyDoesNotPassBlockSignatureValidation_ThrowsBadBlockSignatureConsensusErrorExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));

            // push op_return to note external dependancy in front of pay to pubkey script so it does not match pay to pubkey template.
            // use a different key to generate the script so it does not pass validation.
            var scriptPubKeyOut = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(new Key().PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_ScriptKeyPassesBlockSignatureValidation_DoesNotThrowExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            // push op_return to note external dependancy in front of pay to pubkey script so it does not match pay to pubkey template.
            var scriptPubKeyOut = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_PayToPubKeyScriptPassesBlockSignatureValidation_DoesNotThrowExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            var scriptPubKeyOut = new Script(Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_BlockSignatureEmpty_DoesNotThrowExceptionAsync()
        {
            Block block = Network.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.StratisMain.CreateTransaction());

            Transaction transaction = Network.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(uint256.Zero, uint.MaxValue),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            block.Transactions.Add(transaction);
            (block as PosBlock).BlockSignature = new BlockSignature();

            this.ruleContext.ValidationContext.Block = block;
            Assert.True(BlockStake.IsProofOfWork(this.ruleContext.ValidationContext.Block));

            await this.consensusRules.RegisterRule<PosBlockSignatureRule>().RunAsync(this.ruleContext);
        }
    }
}
