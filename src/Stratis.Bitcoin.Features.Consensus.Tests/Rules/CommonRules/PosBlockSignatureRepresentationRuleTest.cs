using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosBlockSignatureRepresentationRuleTest : TestPosConsensusRulesUnitTestBase
    {
        private Key key;

        public PosBlockSignatureRepresentationRuleTest()
        {
            this.key = new Key();
        }

        [Fact]
        public void Run_IsCanonicalBlockSignature_DoesNotThrowException()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            var scriptPubKeyOut = new Script(Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            // By default the Sign method will give a signature in canonical format.
            ECDSASignature signature = this.key.Sign(block.GetHash());

            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.BlockToValidate = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.BlockToValidate));

            this.consensusRules.RegisterRule<PosBlockSignatureRepresentationRule>().Run(this.ruleContext);
        }

        [Fact]
        public void Run_IsNotCanonicalBlockSignature_ThrowsBadBlockSignatureConsensusErrorException()
        {
            Block block = KnownNetworks.StratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(KnownNetworks.StratisMain.CreateTransaction());

            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();
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

            signature = signature.MakeNonCanonical();

            // Ensure the non-canonical signature is still a valid signature for the block, just in the wrong format.
            Assert.True(this.key.PubKey.Verify(block.GetHash(), signature));
            Assert.False(signature.IsLowS);

            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.BlockToValidate = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.BlockToValidate));

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRepresentationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }
    }
}
