﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// According to BIP34 a coinbase transaction must have the block height serialized in the script language,
    /// </summary>
    /// <remarks>
    /// More info here https://github.com/bitcoin/bips/blob/master/bip-0034.mediawiki
    /// </remarks>        
    public class CoinbaseHeighRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadCoinbaseHeight">Thrown if coinbase doesn't start with serialized block height.</exception>
        public override Task RunAsync(RuleContext context)
        {
            int nHeight = context.BestBlock?.Height + 1 ?? 0;
            Block block = context.BlockValidationContext.Block;

            Script expect = new Script(Op.GetPushOp(nHeight));
            Script actual = block.Transactions[0].Inputs[0].ScriptSig;
            if (!this.StartWith(actual.ToBytes(true), expect.ToBytes(true)))
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_HEIGHT]");
                ConsensusErrors.BadCoinbaseHeight.Throw();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if first <paramref name="subset.Lenght"/> entries are equal between two arrays.
        /// </summary>
        /// <param name="bytes">Main array.</param>
        /// <param name="subset">Subset array.</param>
        /// <returns><c>true</c> if <paramref name="subset.Lenght"/> entries are equal between two arrays. Otherwise <c>false</c>.</returns>
        private bool StartWith(byte[] bytes, byte[] subset)
        {
            if (bytes.Length < subset.Length)
                return false;

            for (int i = 0; i < subset.Length; i++)
            {
                if (subset[i] != bytes[i])
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// With Bitcoin the BIP34 was activated at block 227,835 using the deployment flags, 
    /// this rule allows a chain to have BIP34 activated as a deployment rule.
    /// </summary>
    public class CoinbaseHeighActivationRule : CoinbaseHeighRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            DeploymentFlags deploymentFlags = context.Flags;

            if (deploymentFlags.EnforceBIP34)
            {
                return base.RunAsync(context);
            }

            return Task.CompletedTask;
        }
    }
}