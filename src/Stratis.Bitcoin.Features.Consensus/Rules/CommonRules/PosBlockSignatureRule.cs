﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// A rule that will validate the signature of a PoS block.
    /// </summary>
    public class PosBlockSignatureRule : IntegrityValidationConsensusRule
    {
        /// <summary>When checking the POS block signature this determines the maximum push data (public key) size following the OP_RETURN in the nonspendable output.</summary>
        private const int MaxPushDataSize = 40;

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadBlockSignature">The block signature is invalid.</exception>
        public override void Run(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            if (!(block is PosBlock posBlock))
            {
                this.Logger.LogTrace("(-)[INVALID_CAST]");
                throw new InvalidCastException();
            }

            // Check proof-of-stake block signature.
            if (!this.CheckBlockSignature(posBlock))
            {
                this.Logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.BadBlockSignature.Throw();
            }
        }

        /// <summary>
        /// Checks if block signature is valid.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns><c>true</c> if the signature is valid, <c>false</c> otherwise.</returns>
        private bool CheckBlockSignature(PosBlock block)
        {
            if (BlockStake.IsProofOfWork(block))
            {
                bool res = block.BlockSignature.IsEmpty();
                this.Logger.LogTrace("(-)[POW]:{0}", res);
                return res;
            }

            if (block.BlockSignature.IsEmpty())
            {
                this.Logger.LogTrace("(-)[EMPTY]:false");
                return false;
            }

            TxOut txout = block.Transactions[1].Outputs[1];

            if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(txout.ScriptPubKey))
            {
                PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(txout.ScriptPubKey);
                bool res = pubKey.Verify(block.GetHash(), new ECDSASignature(block.BlockSignature.Signature));
                this.Logger.LogTrace("(-)[P2PK]:{0}", res);
                return res;
            }

            // Block signing key also can be encoded in the nonspendable output.
            // This allows to not pollute UTXO set with useless outputs e.g. in case of multisig staking.

            List<Op> ops = txout.ScriptPubKey.ToOps().ToList();
            if (!ops.Any()) // script.GetOp(pc, opcode, vchPushValue))
            {
                this.Logger.LogTrace("(-)[NO_OPS]:false");
                return false;
            }

            if (ops.ElementAt(0).Code != OpcodeType.OP_RETURN) // OP_RETURN)
            {
                this.Logger.LogTrace("(-)[NO_OP_RETURN]:false");
                return false;
            }

            if (ops.Count != 2)
            {
                this.Logger.LogTrace("(-)[INVALID_OP_COUNT]:false");
                return false;
            }

            byte[] data = ops.ElementAt(1).PushData;

            if (data.Length > MaxPushDataSize)
            {
                this.Logger.LogTrace("(-)[PUSH_DATA_TOO_LARGE]:false");
                return false;
            }

            if (!ScriptEvaluationContext.IsCompressedOrUncompressedPubKey(data))
            {
                this.Logger.LogTrace("(-)[NO_PUSH_DATA]:false");
                return false;
            }

            bool verifyRes = new PubKey(data).Verify(block.GetHash(), new ECDSASignature(block.BlockSignature.Signature));
            return verifyRes;
        }
    }
}