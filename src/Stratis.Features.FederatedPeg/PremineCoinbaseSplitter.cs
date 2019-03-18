using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg
{
    public class PremineCoinbaseSplitter : ICoinbaseSplitter
    {
        /// <summary>
        /// The number of outputs we break the premine reward up into, so that the federation can build more than one transaction at once.
        /// </summary>
        public const int FederationWalletOutputs = 10;

        public void SplitReward(Transaction coinbase)
        {
            TxOut premineOutput = coinbase.Outputs[0];

            Money newTxOutValues = premineOutput.Value / FederationWalletOutputs;
            Script newTxOutScript = premineOutput.ScriptPubKey;

            coinbase.Outputs.Clear();

            for (int i = 0; i < FederationWalletOutputs; i++)
            {
                coinbase.AddOutput(newTxOutValues, newTxOutScript);
            }
        }
    }
}
