using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// A rule that will validate the signature of a PoS block.
    /// </summary>
    /// <remarks>This is partial validation rule.</remarks>
    public class PartialValidationSmartContractPosBlockSignatureRule : AsyncConsensusRule
    {
        private BlockSignatureChecker checker;

        public override void Initialize()
        {
            this.checker = new BlockSignatureChecker();

            base.Initialize();
        }

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadBlockSignature">The block signature is invalid.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            Block block = context.ValidationContext.Block;

            if (!(block is SmartContractPosBlock posBlock))
            {
                this.Logger.LogTrace("(-)[INVALID_CAST]");
                throw new InvalidCastException();
            }

            // Check proof-of-stake block signature.
            if (!this.checker.CheckBlockSignature(posBlock, this.Logger))
            {
                this.Logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.BadBlockSignature.Throw();
            }

            return Task.CompletedTask;
        }
    }

    /// <remarks>This is integrity validation rule.</remarks>
    public class IntegrityValidationSmartContractPosBlockSignatureRule : SyncConsensusRule
    {
        private BlockSignatureChecker checker;

        public override void Initialize()
        {
            this.checker = new BlockSignatureChecker();

            base.Initialize();
        }

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadBlockSignature">The block signature is invalid.</exception>
        public void Run(RuleContext context)
        {
            Block block = context.ValidationContext.Block;

            if (!(block is SmartContractPosBlock posBlock))
            {
                this.Logger.LogTrace("(-)[INVALID_CAST]");
                throw new InvalidCastException();
            }

            // Check proof-of-stake block signature.
            if (!this.checker.CheckBlockSignature(posBlock, this.Logger))
            {
                this.Logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.BadBlockSignature.Throw();
            }
        }
    }

    public class BlockSignatureChecker
    {
        /// <summary>
        /// Checks if block signature is valid.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns><c>true</c> if the signature is valid, <c>false</c> otherwise.</returns>
        public bool CheckBlockSignature(SmartContractPosBlock block, ILogger logger) //TODO ACTIVATION supply logger factory during creation
        {
            logger.LogTrace("()");

            if (BlockStake.IsProofOfWork(block))
            {
                bool res = block.BlockSignature.IsEmpty();
                logger.LogTrace("(-)[POW]:{0}", res);
                return res;
            }

            if (block.BlockSignature.IsEmpty())
            {
                logger.LogTrace("(-)[EMPTY]:false");
                return false;
            }

            TxOut txout = block.Transactions[1].Outputs[1];

            if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(txout.ScriptPubKey))
            {
                PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(txout.ScriptPubKey);
                bool res = pubKey.Verify(block.GetHash(), new ECDSASignature(block.BlockSignature.Signature));
                logger.LogTrace("(-)[P2PK]:{0}", res);
                return res;
            }

            // Block signing key also can be encoded in the nonspendable output.
            // This allows to not pollute UTXO set with useless outputs e.g. in case of multisig staking.

            List<Op> ops = txout.ScriptPubKey.ToOps().ToList();
            if (!ops.Any()) // script.GetOp(pc, opcode, vchPushValue))
            {
                logger.LogTrace("(-)[NO_OPS]:false");
                return false;
            }

            if (ops.ElementAt(0).Code != OpcodeType.OP_RETURN) // OP_RETURN)
            {
                logger.LogTrace("(-)[NO_OP_RETURN]:false");
                return false;
            }

            if (ops.Count < 2) // script.GetOp(pc, opcode, vchPushValue)
            {
                logger.LogTrace("(-)[NO_SECOND_OP]:false");
                return false;
            }

            byte[] data = ops.ElementAt(1).PushData;
            if (!ScriptEvaluationContext.IsCompressedOrUncompressedPubKey(data))
            {
                logger.LogTrace("(-)[NO_PUSH_DATA]:false");
                return false;
            }

            bool verifyRes = new PubKey(data).Verify(block.GetHash(), new ECDSASignature(block.BlockSignature.Signature));
            logger.LogTrace("(-):{0}", verifyRes);
            return verifyRes;
        }
    }
}
