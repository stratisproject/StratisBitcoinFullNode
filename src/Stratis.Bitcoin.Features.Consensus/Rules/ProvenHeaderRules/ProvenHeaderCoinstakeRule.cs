﻿using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
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
        /// <summary>The stake validator.</summary>
        private IStakeValidator stakeValidator;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            Guard.NotNull(this.PosParent.StakeValidator, nameof(this.PosParent.StakeValidator));
            Guard.NotNull(this.PosParent.UtxoSet, nameof(this.PosParent.UtxoSet));

            this.stakeValidator = this.PosParent.StakeValidator;
        }

        /// <inheritdoc />
        protected override void ProcessRule(PosRuleContext context, ChainedHeader chainedHeader, ProvenBlockHeader header)
        {
            this.CheckCoinstakeIsNotNull(header);

            if (header.Coinstake.IsCoinBase)
            {
                // If the header represents a POW block we don't do any validation of stake.
                // We verify the header is not passed the last pow height.

                if (chainedHeader.Height > this.Parent.Network.Consensus.LastPOWBlock)
                {
                    this.Logger.LogTrace("(-)[POW_TOO_HIGH]");
                    ConsensusErrors.ProofOfWorkTooHigh.Throw();
                }

                this.ComputeNextStakeModifier(header, chainedHeader);

                this.Logger.LogTrace("(-)[COIN_BASE_VALIDATION]");
                return;
            }

            this.CheckIfCoinstakeIsTrue(header);

            this.CheckHeaderAndCoinstakeTimes(header);

            FetchCoinsResponse coins = this.GetAndValidateCoins(header, context);
            UnspentOutputs prevUtxo = this.GetAndValidatePreviousUtxo(coins);

            this.CheckCoinstakeAgeRequirement(chainedHeader, prevUtxo);

            this.CheckSignature(header, prevUtxo);

            this.CheckStakeKernelHash(context, prevUtxo, header, chainedHeader);

            this.CheckCoinstakeMerkleProof(header);

            this.CheckHeaderSignatureWithCoinstakeKernel(header);
        }

        /// <summary>
        /// Checks the coinstake to make sure it is not null.
        /// </summary>
        /// <param name="header">The proven blockheader.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.EmptyCoinstake" /> if check fails.
        /// </exception>
        private void CheckCoinstakeIsNotNull(ProvenBlockHeader header)
        {
            if (header.Coinstake != null)
                return;

            this.Logger.LogTrace("(-)[COINSTAKE_IS_NULL]");
            ConsensusErrors.EmptyCoinstake.Throw();
        }

        /// <summary>
        /// Fetches and validates coins from coins view.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="context">Rule context.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.ReadTxPrevFailed" /> if check fails.
        /// </exception>
        private FetchCoinsResponse GetAndValidateCoins(ProvenBlockHeader header, PosRuleContext context)
        {
            // First try finding the previous transaction in database.
            TxIn txIn = header.Coinstake.Inputs[0];
            FetchCoinsResponse coins = this.PosParent.UtxoSet.FetchCoinsAsync(new[] { txIn.PrevOut.Hash }).GetAwaiter().GetResult();
            if ((coins == null) || (coins.UnspentOutputs.Length != 1))
            {
                this.CheckIfCoinstakeIsSpentOnAnotherChain(header, context);
            }

            return coins;
        }

        /// <summary>
        /// Gets and validates unspent outputs based of coins fetched from coin view.
        /// </summary>
        /// <param name="coins">The coins.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.ReadTxPrevFailed" /> if check fails.
        /// </exception>
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

        /// <summary>
        /// Checks if coinstake transaction is valid.
        /// </summary>
        /// <param name="header">The proven block header.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.NonCoinstake" /> if check fails.
        /// </exception>
        private void CheckIfCoinstakeIsTrue(ProvenBlockHeader header)
        {
            if (header.Coinstake.IsCoinStake)
                return;

            this.Logger.LogTrace("(-)[COIN_STAKE_NOT_FOUND]");
            ConsensusErrors.NonCoinstake.Throw();
        }

        /// <summary>
        /// Checks whether header time is equal to the timestamp of the coinstake tx and if coinstake tx
        /// timestamp is divisible by 16 (using timestamp mask).
        /// </summary>
        /// <param name="header">The proven block header.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.StakeTimeViolation" /> if check fails.
        /// </exception>
        private void CheckHeaderAndCoinstakeTimes(ProvenBlockHeader header)
        {
            uint coinstakeTime = header.Coinstake.Time;
            uint headerTime = header.Time;

            // Check if times are equal and coinstake tx time is divisible by 16.
            if ((headerTime == coinstakeTime) && ((coinstakeTime & PosConsensusOptions.StakeTimestampMask) == 0))
                return;

            this.Logger.LogTrace("(-)[BAD_TIME]");
            ConsensusErrors.StakeTimeViolation.Throw();
        }

        /// <summary>
        /// Checks the coinstake age requirement.
        /// </summary>
        /// <param name="chainedHeader">The chained header.</param>
        /// <param name="unspentOutputs">The unspent outputs.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.InvalidStakeDepth" /> if check fails.
        /// </exception>
        private void CheckCoinstakeAgeRequirement(ChainedHeader chainedHeader, UnspentOutputs unspentOutputs)
        {
            ChainedHeader prevChainedHeader = chainedHeader.Previous;

            var options = (PosConsensusOptions)this.PosParent.Network.Consensus.Options;
            int targetDepth = options.GetStakeMinConfirmations(chainedHeader.Height, this.PosParent.Network) - 1;

            if (!this.stakeValidator.IsConfirmedInNPrevBlocks(unspentOutputs, prevChainedHeader, targetDepth))
                return;

            this.Logger.LogTrace("(-)[BAD_STAKE_DEPTH]");
            ConsensusErrors.InvalidStakeDepth.Throw();
        }

        /// <see cref="IStakeValidator.VerifySignature"/>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.CoinstakeVerifySignatureFailed" /> if check fails.
        /// </exception>
        private void CheckSignature(ProvenBlockHeader header, UnspentOutputs unspentOutputs)
        {
            if (this.stakeValidator.VerifySignature(unspentOutputs, header.Coinstake, 0, ScriptVerify.None))
                return;

            this.Logger.LogTrace("(-)[BAD_SIGNATURE]");
            ConsensusErrors.CoinstakeVerifySignatureFailed.Throw();
        }

        private void ComputeNextStakeModifier(ProvenBlockHeader header, ChainedHeader chainedHeader, uint256 previousStakeModifier = null)
        {
            if (previousStakeModifier == null)
                previousStakeModifier = this.GetPreviousStakeModifier(chainedHeader);

            // Computes the stake modifier and sets the value to the current validating proven header,
            // to retain it for next header validation as previousStakeModifier.
            uint256 kernal = header.Coinstake.IsCoinBase ? chainedHeader.HashBlock : header.Coinstake.Inputs[0].PrevOut.Hash;
            header.StakeModifierV2 = this.stakeValidator.ComputeStakeModifierV2(chainedHeader.Previous, previousStakeModifier, kernal);
        }

        private uint256 GetPreviousStakeModifier(ChainedHeader chainedHeader)
        {
            uint256 previousStakeModifier = null;

            ProvenBlockHeader previousProvenHeader = chainedHeader.Previous.Header as ProvenBlockHeader;

            if (previousProvenHeader == null)
            {
                if (chainedHeader.Previous.Height == 0)
                {
                    previousStakeModifier = uint256.Zero;
                    this.Logger.LogTrace("Genesis header.");
                }
                else if (chainedHeader.Previous.Height == this.LastCheckpointHeight)
                {
                    previousStakeModifier = this.LastCheckpoint.StakeModifierV2;
                    this.Logger.LogTrace("Last checkpoint stake modifier V2 loaded: '{0}'.", previousStakeModifier);
                }
                else
                {
                    // When validating a proven header, we expect the previous header be of ProvenBlockHeader type.
                    this.Logger.LogTrace("(-)[PROVEN_HEADER_INVALID_PREVIOUS_HEADER]");
                    ConsensusErrors.InvalidPreviousProvenHeader.Throw();
                }
            }
            else
            {
                previousStakeModifier = previousProvenHeader.StakeModifierV2;
            }

            if (previousStakeModifier == null)
            {
                this.Logger.LogTrace("(-)[PROVEN_HEADER_BAD_PREV_STAKE_MODIFIER]");
                ConsensusErrors.InvalidPreviousProvenHeaderStakeModifier.Throw();
            }

            return previousStakeModifier;
        }

        /// <see cref="IStakeValidator.CheckStakeKernelHash"/>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.PrevStakeNull" /> if check fails.
        /// </exception>
        private void CheckStakeKernelHash(PosRuleContext context, UnspentOutputs stakingCoins, ProvenBlockHeader header, ChainedHeader chainedHeader)
        {
            OutPoint prevOut = this.GetPreviousOut(header);
            uint transactionTime = header.Coinstake.Time;
            uint headerBits = chainedHeader.Header.Bits.ToCompact();

            uint256 previousStakeModifier = this.GetPreviousStakeModifier(chainedHeader);

            if (header.Coinstake.IsCoinStake)
            {
                this.Logger.LogTrace("Found coinstake checking kernal hash.");
                this.stakeValidator.CheckStakeKernelHash(context, headerBits, previousStakeModifier, stakingCoins, prevOut, transactionTime);
            }

            this.ComputeNextStakeModifier(header, chainedHeader, previousStakeModifier);
        }

        /// <summary>
        /// Check that coinstake tx is in the merkle tree using the merkle proof.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.BadMerkleRoot" /> if check fails.
        /// </exception>
        private void CheckCoinstakeMerkleProof(ProvenBlockHeader header)
        {
            if (header.MerkleProof.Check(header.HashMerkleRoot))
                return;

            this.Logger.LogTrace("(-)[BAD_MERKLE_ROOT]");
            ConsensusErrors.BadMerkleRoot.Throw();
        }

        /// <summary>
        /// Verifies header signature with the key from coinstake kernel.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.BadBlockSignature" /> if check fails.
        /// </exception>
        private void CheckHeaderSignatureWithCoinstakeKernel(ProvenBlockHeader header)
        {
            Script script = header.Coinstake.Outputs[1].ScriptPubKey;
            PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script);

            var signature = new ECDSASignature(header.Signature.Signature);
            uint256 headerHash = header.GetHash();

            if (pubKey.Verify(headerHash, signature))
                return;

            this.Logger.LogTrace("(-)[BAD_HEADER_SIGNATURE]");
            ConsensusErrors.BadBlockSignature.Throw();
        }

        /// <summary>
        /// Checks if coinstake is spent on another chain.
        /// </summary>
        /// <param name="header">The proven block header.</param>
        /// <param name="context">The POS rule context.</param>
        /// <exception cref="ConsensusException">Throws utxo not found error.</exception>
        private void CheckIfCoinstakeIsSpentOnAnotherChain(ProvenBlockHeader header, PosRuleContext context)
        {
            Transaction coinstake = header.Coinstake;
            TxIn input = coinstake.Inputs[0];

            int? rewindDataIndex = this.PosParent.RewindDataIndexStore.Get(input.PrevOut.Hash, (int)input.PrevOut.N);
            if (!rewindDataIndex.HasValue)
            {
                context.ValidationContext.SetFlagAndThrow(ConsensusErrors.ReadTxPrevFailed, c => c.InsufficientHeaderInformation = true);
            }

            RewindData rewindData = this.PosParent.UtxoSet.GetRewindData(rewindDataIndex.Value).GetAwaiter().GetResult();
            UnspentOutputs matchingUnspentUtxo = 
                rewindData.OutputsToRestore.Where((unspent, i) => (unspent.TransactionId == input.PrevOut.Hash) && (i == input.PrevOut.N)).FirstOrDefault();

            if (matchingUnspentUtxo == null)
            {
                context.ValidationContext.SetFlagAndThrow(ConsensusErrors.UtxoNotFoundInRewindData, ct => ct.InsufficientHeaderInformation = true);
            }

            this.CheckHeaderSignatureWithCoinstakeKernel(header);
        }

        private OutPoint GetPreviousOut(ProvenBlockHeader header)
        {
            TxIn input = header.Coinstake.Inputs[0];
            OutPoint prevOut = input.PrevOut;

            return prevOut;
        }
    }
}
