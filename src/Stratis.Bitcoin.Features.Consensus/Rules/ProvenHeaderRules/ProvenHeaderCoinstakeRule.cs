using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <inheritdoc />
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
        /// <summary>Coinstale validator instance.</summary>
        private IStakeValidator stakeValidator;

        /// <summary>Coin view.</summary>
        private ICoinView coinView;

        /// <summary>PoS block's timestamp mask.</summary>
        /// <remarks>Used to decrease granularity of timestamp. Supposed to be 2^n-1.</remarks>
        public const uint StakeTimestampMask = 0x0000000F;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            Guard.NotNull(this.PosParent.StakeValidator, nameof(this.PosParent.StakeValidator));
            Guard.NotNull(this.PosParent.UtxoSet, nameof(this.PosParent.UtxoSet));

            this.stakeValidator = this.PosParent.StakeValidator;
            this.coinView = this.PosParent.UtxoSet;
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
    }
}
