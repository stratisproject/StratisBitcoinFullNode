using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
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

        /// <inheritdoc/>
        public override void Run(RuleContext context)
        {
            Guard.NotNull(context.ValidationContext.ChainedHeaderToValidate, nameof(context.ValidationContext.ChainedHeaderToValidate));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;
            int height = chainedHeader.Height;

            if (context.SkipValidation || !this.IsProvenHeaderActivated(height))
                return;

            var header = (ProvenBlockHeader)chainedHeader.Header;

            this.CheckCoinstakeIsNotNull(header);

            this.CheckIfCoinstakeIsTrue(header);

            this.CheckHeaderAndCoinstakeTimes(header);

            FetchCoinsResponse coins = this.GetAndValidateCoins(header);
            UnspentOutputs prevUtxo = this.GetAndValidatePreviousUtxo(coins);

            this.CheckCoinstakeAgeRequirement(chainedHeader, prevUtxo);

            this.CheckSignature(header, prevUtxo);

            this.ComputeStakeModifier((PosRuleContext)context, header, chainedHeader);

            this.CheckStakeKernelHash((PosRuleContext)context, prevUtxo, header, chainedHeader);

            this.CheckCoinstakeMerkleProof(header);

            this.CheckHeaderSignatureWithCoinstakeKernel(header, prevUtxo);
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
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.ReadTxPrevFailed" /> if check fails.
        /// </exception>
        private FetchCoinsResponse GetAndValidateCoins(ProvenBlockHeader header)
        {
            // First try finding the previous transaction in database.
            TxIn txIn = header.Coinstake.Inputs[0];
            FetchCoinsResponse coins = this.PosParent.UtxoSet.FetchCoinsAsync(new[] { txIn.PrevOut.Hash }).GetAwaiter().GetResult();
            if ((coins == null) || (coins.UnspentOutputs.Length != 1))
                ConsensusErrors.ReadTxPrevFailed.Throw();

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

            if (this.stakeValidator.IsConfirmedInNPrevBlocks(unspentOutputs, prevChainedHeader, targetDepth))
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

        /// <summary>
        /// Computes the stake modifier.
        /// </summary>
        /// <param name="header">The proven header to be validated.</param>
        /// <param name="previousHeader">The previous header.</param>
        private void ComputeStakeModifier(PosRuleContext context, ProvenBlockHeader header, ChainedHeader chainedHeader)
        {
            uint256 previousStakeModifier = null;

            int lastCheckpointHeight = this.Parent.Checkpoints.GetLastCheckpointHeight();
            // We are ahead of last checkpoint so we should always have at a StakeModifier
            if (chainedHeader.Height > lastCheckpointHeight)
            {
                // Compute stake modifier.
                ChainedHeader prevChainedHeader = chainedHeader.Previous;
                BlockStake blockStakePrev = prevChainedHeader == null ? null : this.stakeChain.Get(prevChainedHeader.HashBlock);
                blockStake.StakeModifierV2 = this.stakeValidator.ComputeStakeModifierV2(prevChainedHeader, blockStakePrev?.StakeModifierV2, blockStake.IsProofOfWork() ? chainedHeader.HashBlock : blockStake.PrevoutStake.Hash);
            }
            else if (chainedHeader.Height == lastCheckpointHeight)
            {
                // Copy checkpointed stake modifier.
                CheckpointInfo checkpoint = this.Parent.Checkpoints.GetCheckpoint(lastCheckpointHeight);
                header.StakeModifierV2 = checkpoint.StakeModifierV2;
                this.Logger.LogTrace("Last checkpoint stake modifier V2 loaded: '{0}'.", header.StakeModifierV2);
            }
            else
            {
                this.Logger.LogTrace("POS stake modifier computation skipped for block at height {0} because it is not above last checkpoint block height {1}.", chainedHeader.Height, lastCheckpointHeight);
            }


            // If current rule context doesn't have yet a BlockStake, try to get from the store
            context.BlockStake = context.BlockStake ?? this.PosParent.StakeChain.Get(previousHeader.HashBlock);

            ChainedHeader prevChainedHeader = chainedHeader.Previous;
            BlockStake blockStakePrev = prevChainedHeader == null ? null : this.stakeChain.Get(prevChainedHeader.HashBlock);
            blockStake.StakeModifierV2 = this.stakeValidator.ComputeStakeModifierV2(prevChainedHeader, blockStakePrev?.StakeModifierV2, blockStake.IsProofOfWork() ? chainedHeader.HashBlock : blockStake.PrevoutStake.Hash);
        }

        /// <see cref="IStakeValidator.CheckStakeKernelHash"/>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.PrevStakeNull" /> if check fails.
        /// </exception>
        private void CheckStakeKernelHash(PosRuleContext context, UnspentOutputs stakingCoins, ProvenBlockHeader header, ChainedHeader chainedHeader)
        {
            OutPoint prevOut = this.GetPreviousOut(header);
            uint transactionTime = header.Coinstake.Time;

            ChainedHeader prevChainedHeader = chainedHeader.Previous;

            uint256 stakeModifierV2;
            BlockStake prevBlockStake = this.PosParent.StakeChain.Get(prevChainedHeader.HashBlock);
            if (prevBlockStake != null)
            {
                stakeModifierV2 = prevBlockStake.StakeModifierV2;
            }
            else
            {
                if (prevChainedHeader.Header is ProvenBlockHeader phHeader)
                {
                    stakeModifierV2 = phHeader.StakeModifierV2;
                }
                else
                {
                    stakeModifierV2 = null;
                    this.Logger.LogTrace("(-)[BAD_PREV_STAKE]");
                    ConsensusErrors.PrevStakeNull.Throw();
                }
            }

            uint headerBits = chainedHeader.Header.Bits.ToCompact();

            this.stakeValidator.CheckStakeKernelHash(context, headerBits, prevBlockStake.StakeModifierV2, stakingCoins, prevOut, transactionTime);
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
        /// <param name="stakingCoins">The staking coins.</param>
        /// <exception cref="ConsensusException">
        /// Throws exception with error <see cref="ConsensusErrors.BadBlockSignature" /> if check fails.
        /// </exception>
        private void CheckHeaderSignatureWithCoinstakeKernel(ProvenBlockHeader header, UnspentOutputs stakingCoins)
        {
            OutPoint prevOut = this.GetPreviousOut(header);

            Script scriptPubKey = stakingCoins.Outputs[prevOut.N].ScriptPubKey;
            PubKey pubKey = scriptPubKey.GetDestinationPublicKeys(this.PosParent.Network)[0];

            var signature = new ECDSASignature(header.Signature.Signature);
            uint256 headerHash = header.GetHash();

            if (pubKey.Verify(headerHash, signature))
                return;

            this.Logger.LogTrace("(-)[BAD_HEADER_SIGNATURE]");
            ConsensusErrors.BadBlockSignature.Throw();
        }

        private OutPoint GetPreviousOut(ProvenBlockHeader header)
        {
            TxIn input = header.Coinstake.Inputs[0];
            OutPoint prevOut = input.PrevOut;

            return prevOut;
        }
    }
}
