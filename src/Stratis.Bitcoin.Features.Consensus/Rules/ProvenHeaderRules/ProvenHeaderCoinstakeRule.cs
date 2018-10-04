using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <summary>
    /// Rule to check if the coinstake inside proven header is valid:
    /// <list type="number">
    ///     <item><description>Check that coinstake tx has IsCoinStake property equal to true.</description></item>
    ///     <item><description>Header time is equal to the timestamp of the coinstake tx.</description></item>
    ///     <item><description>Check if coinstake tx timestamp is divisible by 16 (using timestamp mask).</description></item>
    ///     <item><description>Verify the coinstake age requirement (*).</description></item>
    ///     <item>
    ///     <description>
    ///         Verify all coinstake transaction inputs (*).
    ///         <list type="number">
    ///             <item><description>Input comes from a UTXO.</description></item>
    ///             <item><description>Verify the ScriptSig</description></item>
    ///         </list>
    ///     </description>
    ///     </item>
    ///     <item><description>Check if the coinstake kernel hash satisfies the difficulty requirement (*).</description></item>
    ///     <item><description>Check that coinstake tx is in the merkle tree using the merkle proof.</description></item>
    ///     <item><description>Verify header signature with the key from coinstake kernel (*).</description></item>
    /// </list>
    /// </summary>
    /// <remarks>(*) - denotes rules that are expensive to execute.</remarks>
    /// <seealso cref="T:Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules.ProvenHeaderRuleBase" />
    public class ProvenHeaderCoinstakeRule : ProvenHeaderRuleBase
    {
        /// <summary>PoS block's timestamp mask.</summary>
        /// <remarks>Used to decrease granularity of timestamp. Supposed to be 2^n-1.</remarks>
        public const uint StakeTimestampMask = 0x0000000F;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            Guard.NotNull(this.PosParent.StakeValidator, nameof(this.PosParent.StakeValidator));
            Guard.NotNull(this.PosParent.UtxoSet, nameof(this.PosParent.UtxoSet));
        }

        public override void Run(RuleContext context)
        {
            Guard.NotNull(context.ValidationContext.ChainedHeaderToValidate, nameof(context.ValidationContext.ChainedHeaderToValidate));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;
            int height = chainedHeader.Height;

            if (context.SkipValidation || !this.IsProvenHeaderActivated(height))
                return;

            var header = (ProvenBlockHeader)chainedHeader.Header;

            this.CheckCoinstakeIsNotNull(header);

            FetchCoinsResponse coins = this.GetAndValidateCoins(header);
            UnspentOutputs prevUtxo = this.GetAndValidatePreviousUtxo(coins);

            this.CheckIfCoinstakeIsTrue(header);

            this.CheckHeaderAndCoinstakeTimes(header);

            this.CheckCoinstakeAgeRequirement(chainedHeader, prevUtxo);

            this.CheckSignature(header, prevUtxo);

            this.CheckStakeKernelHash((PosRuleContext)context, prevUtxo, header, chainedHeader);

            this.CheckCoinstakeMerkleProof(header);

            this.CheckHeaderSignatureWithConinstakeKernel(header, prevUtxo);
        }

        private void CheckCoinstakeIsNotNull(ProvenBlockHeader header)
        {
            if (header.Coinstake != null)
                return;

            this.Logger.LogTrace("(-)[COINSTAKE_IS_NULL]");
            ConsensusErrors.EmptyCoinstake.Throw();
        }

        private FetchCoinsResponse GetAndValidateCoins(ProvenBlockHeader header)
        {
            // First try finding the previous transaction in database.
            TxIn txIn = header.Coinstake.Inputs[0];
            FetchCoinsResponse coins = this.PosParent.UtxoSet.FetchCoinsAsync(new[] { txIn.PrevOut.Hash }).GetAwaiter().GetResult();
            if ((coins == null) || (coins.UnspentOutputs.Length != 1))
                ConsensusErrors.ReadTxPrevFailed.Throw();

            return coins;
        }

        private UnspentOutputs GetAndValidatePreviousUtxo(FetchCoinsResponse coins)
        {
            UnspentOutputs prevUtxo = coins.UnspentOutputs[0];
            if (prevUtxo == null)
            {
                this.Logger.LogTrace("(-)[PREV_UTXO_IS_NULL]");
                ConsensusErrors.ReadTxPrevFailed.Throw();
            }

            return prevUtxo;
        }

        private void CheckIfCoinstakeIsTrue(ProvenBlockHeader header)
        {
            if (header.Coinstake.IsCoinStake)
                return;

            this.Logger.LogTrace("(-)[PROVEN_HEADER_INVALID_MERKLE_PROOF_SIZE]");
            ConsensusErrors.NonCoinstake.Throw();
        }

        private void CheckHeaderAndCoinstakeTimes(ProvenBlockHeader header)
        {
            uint coinstakeTime = header.Coinstake.Time;
            uint headerTime = header.Time; 

            // Check if times are equal and coinstake tx time is divisible by 16.
            if ((headerTime == coinstakeTime) && ((coinstakeTime & StakeTimestampMask) == 0))
                return;

            this.Logger.LogTrace("(-)[BAD_TIME]");
            ConsensusErrors.StakeTimeViolation.Throw();
        }

        private void CheckCoinstakeAgeRequirement(ChainedHeader chainedHeader, UnspentOutputs unspentOutputs)
        {
            ChainedHeader prevChainedHeader = chainedHeader.Previous;

            var options = (PosConsensusOptions)this.PosParent.Network.Consensus.Options;
            int targetDepth = options.GetStakeMinConfirmations(prevChainedHeader.Height + 1, this.PosParent.Network) - 1;

            int actualDepth = prevChainedHeader.Height - (int)unspentOutputs.Height;
            if (actualDepth < targetDepth)
                return;

            this.Logger.LogTrace("(-)[BAD_STAKE_DEPTH]");
            ConsensusErrors.InvalidStakeDepth.Throw();
        }

        private void CheckSignature(ProvenBlockHeader header, UnspentOutputs unspentOutputs)
        {
            TxIn input = header.Coinstake.Inputs[0];

            if ((input.PrevOut.N >= unspentOutputs.Outputs.Length) || (input.PrevOut.Hash != unspentOutputs.TransactionId))
            {
                this.Logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.CoinstakeVerifySignatureFailed.Throw();
            }

            TxOut output = unspentOutputs.Outputs[input.PrevOut.N];

            var txData = new PrecomputedTransactionData(header.Coinstake);
            var checker = new TransactionChecker(header.Coinstake, 0, output.Value, txData);
            var ctx = new ScriptEvaluationContext(this.PosParent.Network) { ScriptVerify = ScriptVerify.None };

            if (ctx.VerifyScript(input.ScriptSig, output.ScriptPubKey, checker))
                return;

            this.Logger.LogTrace("(-)[BAD_SIGNATURE]");
            ConsensusErrors.CoinstakeVerifySignatureFailed.Throw();
        }

        private void CheckStakeKernelHash(PosRuleContext context, UnspentOutputs stakingCoins, ProvenBlockHeader header, ChainedHeader chainedHeader)
        {
            TxIn input = header.Coinstake.Inputs[0];
            OutPoint prevout = input.PrevOut;
            uint transactionTime = header.Coinstake.Time;

            ChainedHeader prevChainedHeader = chainedHeader.Previous;

            BlockStake prevBlockStake = this.PosParent.StakeChain.Get(prevChainedHeader.HashBlock);
            if (prevBlockStake == null)
            {
                this.Logger.LogTrace("(-)[BAD_PREV_STAKE]");
                ConsensusErrors.PrevStakeNull.Throw();
            }

            uint headerBits = chainedHeader.Header.Bits.ToCompact();

            if (transactionTime < stakingCoins.Time)
            {
                this.Logger.LogTrace("Coinstake transaction timestamp {0} is lower than it's own UTXO timestamp {1}.", transactionTime, stakingCoins.Time);
                this.Logger.LogTrace("(-)[BAD_STAKE_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }

            // Base target.
            BigInteger target = new Target(headerBits).ToBigInteger();

            // TODO: Investigate:
            // The POS protocol should probably put a limit on the max amount that can be staked
            // not a hard limit but a limit that allow any amount to be staked with a max weight value.
            // The max weight should not exceed the max uint256 array size (array size = 32).

            // Weighted target.
            long valueIn = stakingCoins.Outputs[prevout.N].Value.Satoshi;
            BigInteger weight = BigInteger.ValueOf(valueIn);
            BigInteger weightedTarget = target.Multiply(weight);

            context.TargetProofOfStake = this.ToUInt256(weightedTarget);
            this.Logger.LogTrace("POS target is '{0}', weighted target for {1} coins is '{2}'.", this.ToUInt256(target), valueIn, context.TargetProofOfStake);

            // ReSharper disable once PossibleNullReferenceException - it is checked above.
            uint256 stakeModifierV2 = prevBlockStake.StakeModifierV2;

            // Calculate hash.
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(stakeModifierV2);
                serializer.ReadWrite(stakingCoins.Time);
                serializer.ReadWrite(prevout.Hash);
                serializer.ReadWrite(prevout.N);
                serializer.ReadWrite(transactionTime);

                context.HashProofOfStake = Hashes.Hash256(ms.ToArray());
            }

            this.Logger.LogTrace("Stake modifier V2 is '{0}', hash POS is '{1}'.", stakeModifierV2, context.HashProofOfStake);

            // Now check if proof-of-stake hash meets target protocol.
            var hashProofOfStakeTarget = new BigInteger(1, context.HashProofOfStake.ToBytes(false));
            if (hashProofOfStakeTarget.CompareTo(weightedTarget) > 0)
            {
                this.Logger.LogTrace("(-)[TARGET_MISSED]");
                ConsensusErrors.StakeHashInvalidTarget.Throw();
            }
        }

        private void CheckCoinstakeMerkleProof(ProvenBlockHeader header)
        {
            if (header.MerkleProof.Check(header.HashMerkleRoot))
                return;

            this.Logger.LogTrace("(-)[BAD_MERKLE_ROOT]");
            ConsensusErrors.BadMerkleRoot.Throw();
        }

        private void CheckHeaderSignatureWithConinstakeKernel(ProvenBlockHeader header, UnspentOutputs stakingCoins)
        {
            TxIn input = header.Coinstake.Inputs[0];
            OutPoint prevout = input.PrevOut;

            Script scriptPubKey = stakingCoins.Outputs[prevout.N].ScriptPubKey;
            PubKey pubKey = scriptPubKey.GetDestinationPublicKeys(this.PosParent.Network)[0];

            byte[] signature = header.Signature.Signature;
            uint256 headerHash = header.GetHash();

            if (pubKey.Verify(headerHash, signature))
                return;

            this.Logger.LogTrace("(-)[BAD_HEADER_SIGNATURE]");
            ConsensusErrors.BadBlockSignature.Throw();
        }

        /// <summary>
        /// Converts <see cref="BigInteger" /> to <see cref="uint256" />.
        /// </summary>
        /// <param name="input"><see cref="BigInteger"/> input value.</param>
        /// <returns><see cref="uint256"/> version of <paramref name="input"/>.</returns>
        private uint256 ToUInt256(BigInteger input)
        {
            byte[] array = input.ToByteArray();

            int missingZero = 32 - array.Length;
            if (missingZero < 0)
            {
                //throw new InvalidOperationException("Awful bug, this should never happen");
                array = array.Skip(Math.Abs(missingZero)).ToArray();
            }

            if (missingZero > 0)
                array = new byte[missingZero].Concat(array).ToArray();

            return new uint256(array, false);
        }
    }
}
