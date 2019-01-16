using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
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

            // In case we are in PoW era there might be no coinstake tx.
            // We have no way of telling if the block was supposed to be PoW or PoS so attacker can trick us into thinking that all of them are PoW so no PH is required.
            if (!header.Coinstake.IsCoinStake)
            {
                // If the header represents a POW block we don't do any validation of stake.
                // We verify the header is not passed the last pow height.
                if (chainedHeader.Height > this.Parent.Network.Consensus.LastPOWBlock)
                {
                    this.Logger.LogTrace("(-)[POW_TOO_HIGH]");
                    ConsensusErrors.ProofOfWorkTooHigh.Throw();
                }

                if (!header.CheckProofOfWork())
                {
                    this.Logger.LogTrace("(-)[HIGH_HASH]");
                    ConsensusErrors.HighHash.Throw();
                }

                this.ComputeNextStakeModifier(header, chainedHeader);

                this.Logger.LogTrace("(-)[POW_ERA]");
                return;
            }

            this.CheckIfCoinstakeIsTrue(header);

            this.CheckHeaderAndCoinstakeTimes(header);

            UnspentOutputs prevUtxo = this.GetAndValidatePreviousUtxo(header, context);

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
            if (header.Coinstake == null)
            {
                this.Logger.LogTrace("(-)[COINSTAKE_IS_NULL]");
                ConsensusErrors.EmptyCoinstake.Throw();
            }
        }

        /// <summary>
        /// Gets and validates unspent outputs based of coins fetched from coin view.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="context">Rule context.</param>
        /// <returns>The validated previous <see cref="UnspentOutputs"/></returns>
        private UnspentOutputs GetAndValidatePreviousUtxo(ProvenBlockHeader header, PosRuleContext context)
        {
            // First try and find the previous trx in the database.
            TxIn txIn = header.Coinstake.Inputs[0];

            UnspentOutputs prevUtxo = null;

            FetchCoinsResponse coins = this.PosParent.UtxoSet.FetchCoinsAsync(new[] { txIn.PrevOut.Hash }).GetAwaiter().GetResult();
            if (coins.UnspentOutputs[0] == null)
            {
                // We did not find the previous trx in the database, look in rewind data.
                prevUtxo = this.CheckIfCoinstakeIsSpentOnAnotherChain(header, context);
            }
            else
            {
                // The trx was found now check if the UTXO is spent.
                prevUtxo = coins.UnspentOutputs[0];

                TxOut utxo = null;
                if (txIn.PrevOut.N < prevUtxo.Outputs.Length)
                {
                    // Check that the size of the outs collection is the same as the expected position of the UTXO
                    // Note the collection will not always represent the original size of the transaction unspent
                    // outputs because when we store outputs do disk the last spent items are removed from the collection.
                    utxo = prevUtxo.Outputs[txIn.PrevOut.N];
                }

                if (utxo == null)
                {
                    // UTXO is spent so find it in rewind data.
                    prevUtxo = this.CheckIfCoinstakeIsSpentOnAnotherChain(header, context);
                }
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
            if (!header.Coinstake.IsCoinStake)
            {
                this.Logger.LogTrace("(-)[COIN_STAKE_NOT_FOUND]");
                ConsensusErrors.NonCoinstake.Throw();
            }
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

            if ((headerTime != coinstakeTime) || ((coinstakeTime & PosConsensusOptions.StakeTimestampMask) != 0))
            {
                this.Logger.LogTrace("(-)[BAD_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }
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

            if (this.stakeValidator.IsConfirmedInNPrevBlocks(unspentOutputs, prevChainedHeader, targetDepth))
            {
                this.Logger.LogTrace("(-)[BAD_STAKE_DEPTH]");
                ConsensusErrors.InvalidStakeDepth.Throw();
            }
        }

        /// <see cref="IStakeValidator.VerifySignature"/>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.CoinstakeVerifySignatureFailed" /> if check fails.
        /// </exception>
        private void CheckSignature(ProvenBlockHeader header, UnspentOutputs unspentOutputs)
        {
            if (!this.stakeValidator.VerifySignature(unspentOutputs, header.Coinstake, 0, ScriptVerify.None))
            {
                this.Logger.LogTrace("(-)[BAD_SIGNATURE]");
                ConsensusErrors.CoinstakeVerifySignatureFailed.Throw();
            }
        }

        private void ComputeNextStakeModifier(ProvenBlockHeader header, ChainedHeader chainedHeader, uint256 previousStakeModifier = null)
        {
            if (previousStakeModifier == null)
                previousStakeModifier = this.GetPreviousStakeModifier(chainedHeader);

            // Computes the stake modifier and sets the value to the current validating proven header,
            // to retain it for next header validation as previousStakeModifier.
            uint256 hash = !header.Coinstake.IsCoinStake ? chainedHeader.HashBlock : header.Coinstake.Inputs[0].PrevOut.Hash;
            header.StakeModifierV2 = this.stakeValidator.ComputeStakeModifierV2(chainedHeader.Previous, previousStakeModifier, hash);
        }

        private uint256 GetPreviousStakeModifier(ChainedHeader chainedHeader)
        {
            var previousProvenHeader = chainedHeader.Previous.Header as ProvenBlockHeader;

            if (previousProvenHeader != null)
            {
                if (previousProvenHeader.StakeModifierV2 == null)
                {
                    this.Logger.LogTrace("(-)[MODIF_IS_NULL]");
                    ConsensusErrors.InvalidPreviousProvenHeaderStakeModifier.Throw();
                }

                //Stake modifier acquired from prev PH.
                this.Logger.LogTrace("(-)[PREV_PH]");
                return previousProvenHeader.StakeModifierV2;
            }

            if (chainedHeader.Previous.Height == 0)
            {
                this.Logger.LogTrace("(-)[GENESIS]");
                return uint256.Zero;
            }

            if (chainedHeader.Previous.Height == this.LastCheckpointHeight)
            {
                this.Logger.LogTrace("(-)[FROM_CHECKPOINT]");
                return this.LastCheckpoint.StakeModifierV2;
            }

            uint256 previousStakeModifier = this.PosParent.StakeChain.Get(chainedHeader.Previous.HashBlock)?.StakeModifierV2;

            if (previousStakeModifier == null)
            {
                // When validating a proven header, we expect the previous header be of ProvenBlockHeader type.
                // If this is not the case we will need to investigate why PH wasn't assigned. This might be due to
                // the logic in ProvenHeadersBlockStoreSignaled.CreateAndStoreProvenHeader.
                this.Logger.LogTrace("(-)[PROVEN_HEADER_INVALID_PREVIOUS_HEADER]");
                ConsensusErrors.InvalidPreviousProvenHeader.Throw();
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

                var validKernel = this.stakeValidator.CheckStakeKernelHash(context, headerBits, previousStakeModifier, stakingCoins, prevOut, transactionTime);

                if (!validKernel)
                {
                    this.Logger.LogTrace("(-)[INVALID_STAKE_HASH_TARGET]");
                    ConsensusErrors.StakeHashInvalidTarget.Throw();
                }
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
            if (!header.MerkleProof.Check(header.HashMerkleRoot))
            {
                this.Logger.LogTrace("(-)[BAD_MERKLE_ROOT]");
                ConsensusErrors.BadMerkleRoot.Throw();
            }
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
            var consensusRules = (PosConsensusRuleEngine)this.Parent;
            if (!consensusRules.StakeValidator.CheckStakeSignature(header.Signature, header.GetHash(), header.Coinstake))
            {
                this.Logger.LogTrace("(-)[BAD_HEADER_SIGNATURE]");
                ConsensusErrors.BadBlockSignature.Throw();
            }
        }

        /// <summary>
        /// Checks if coinstake is spent on another chain.
        /// </summary>
        /// <param name="header">The proven block header.</param>
        /// <param name="context">The POS rule context.</param>
        /// <returns>The <see cref="UnspentOutputs"> found in a RewindData</returns>
        private UnspentOutputs CheckIfCoinstakeIsSpentOnAnotherChain(ProvenBlockHeader header, PosRuleContext context)
        {
            Transaction coinstake = header.Coinstake;
            TxIn input = coinstake.Inputs[0];

            int? rewindDataIndex = this.PosParent.RewindDataIndexCache.Get(input.PrevOut.Hash, (int)input.PrevOut.N);
            if (!rewindDataIndex.HasValue)
            {
                this.Logger.LogTrace("(-)[NO_REWIND_DATA_INDEX_FOR_INPUT_PREVOUT]");
                context.ValidationContext.InsufficientHeaderInformation = true;
                ConsensusErrors.ReadTxPrevFailedInsufficient.Throw();
            }

            RewindData rewindData = this.PosParent.UtxoSet.GetRewindData(rewindDataIndex.Value).GetAwaiter().GetResult();

            if (rewindData == null)
            {
                this.Logger.LogTrace("(-)[NO_REWIND_DATA_FOR_INDEX]");
                this.Logger.LogError("Error - Rewind data should always be present");
                throw new ConsensusException("Rewind data should always be present");
            }

            UnspentOutputs matchingUnspentUtxo = null;
            foreach (UnspentOutputs unspent in rewindData.OutputsToRestore)
            {
                if (unspent.TransactionId == input.PrevOut.Hash)
                {
                    if (input.PrevOut.N < unspent.Outputs.Length)
                    {
                        matchingUnspentUtxo = unspent;
                        break;
                    }
                }
            }

            if (matchingUnspentUtxo == null)
            {
                this.Logger.LogTrace("(-)[UTXO_NOT_FOUND_IN_REWIND_DATA]");
                context.ValidationContext.InsufficientHeaderInformation = true;
                ConsensusErrors.UtxoNotFoundInRewindData.Throw();
            }

            return matchingUnspentUtxo;
        }

        private OutPoint GetPreviousOut(ProvenBlockHeader header)
        {
            TxIn input = header.Coinstake.Inputs[0];
            OutPoint prevOut = input.PrevOut;

            return prevOut;
        }
    }
}
