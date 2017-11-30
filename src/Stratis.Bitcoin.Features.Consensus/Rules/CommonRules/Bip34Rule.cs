using System.Threading.Tasks;
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
    public class Bip34Rule : SkipValidationConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(ContextInformation context)
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
        /// Compare two byte arrays and return <c>true</c> if the first array start with the same sequence bytes as the second array from the first position.
        /// </summary>
        /// <param name="bytes">The first array in the checking sequence.</param>
        /// <param name="subset">The second array in the checking sequence.</param>
        /// <returns><c>true</c> if the second array has the same elements as the first array from the first position.</returns>
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
    public class Bip34ActivationRule : Bip34Rule
    {
        /// <inheritdoc />
        public override Task RunAsync(ContextInformation context)
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