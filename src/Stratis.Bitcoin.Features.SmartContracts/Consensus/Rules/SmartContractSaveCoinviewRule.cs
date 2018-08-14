﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <remarks>This is partial validation rule</remarks>
    public sealed class SmartContractSaveCoinviewRule : UtxoStoreConsensusRule
    {
        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            uint256 oldBlockHash = context.ValidationContext.ChainTipToExtend.Previous.HashBlock;
            uint256 nextBlockHash = context.ValidationContext.ChainTipToExtend.HashBlock;

            // Persist the changes to the coinview. This will likely only be stored in memory,
            // unless the coinview treashold is reached.
            this.Logger.LogTrace("Saving coinview changes.");
            var utxoRuleContext = context as UtxoRuleContext;
            await this.PowParent.UtxoSet.SaveChangesAsync(utxoRuleContext.UnspentOutputSet.GetCoins(this.PowParent.UtxoSet), null, oldBlockHash, nextBlockHash).ConfigureAwait(false);
        }
    }
}
