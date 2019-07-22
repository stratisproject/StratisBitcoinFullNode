using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// Exposes methods that help with the identifying and signing of transactions deterministically.
    /// </summary>
    public static class SigningUtils
    {
        public static int GetSignatureCount(this Transaction partialTransaction, Network network)
        {
            Guard.NotNull(partialTransaction, nameof(partialTransaction));
            Guard.Assert(partialTransaction.Inputs.Any());

            Script scriptSig = partialTransaction.Inputs[0].ScriptSig;
            if (scriptSig == null)
                return 0;

            // Remove the script from the end.
            scriptSig = new Script(scriptSig.ToOps().SkipLast(1));

            TransactionSignature[] result = PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(network, scriptSig);

            return result?.Count(s => s != null) ?? 0;
        }

        public static Transaction CheckTemplateAndCombineSignatures(TransactionBuilder builder, Transaction existingTransaction, Transaction[] partialTransactions)
        {
            Transaction[] validPartials = partialTransactions.Where(p => TemplatesMatch(builder.Network, p, existingTransaction) && p.GetHash() != existingTransaction.GetHash()).ToArray();
            if (validPartials.Any())
            {
                var allPartials = new Transaction[validPartials.Length + 1];
                allPartials[0] = existingTransaction;
                validPartials.CopyTo(allPartials, 1);

                existingTransaction = builder.CombineSignatures(true, allPartials);
            }

            return existingTransaction;
        }


        /// <summary>
        /// Checks whether two transactions have identical inputs and outputs.
        /// </summary>
        /// <param name="partialTransaction1">First transaction.</param>
        /// <param name="partialTransaction2">Second transaction.</param>
        /// <returns><c>True</c> if identical and <c>false</c> otherwise.</returns>
        public static bool TemplatesMatch(Network network, Transaction partialTransaction1, Transaction partialTransaction2)
        {
            if (network.Consensus.IsProofOfStake)
            {
                if (partialTransaction1.Time != partialTransaction2.Time)
                {
                    return false;
                }
            }

            if ((partialTransaction1.Inputs.Count != partialTransaction2.Inputs.Count) ||
                (partialTransaction1.Outputs.Count != partialTransaction2.Outputs.Count))
            {
                return false;
            }

            for (int i = 0; i < partialTransaction1.Inputs.Count; i++)
            {
                TxIn input1 = partialTransaction1.Inputs[i];
                TxIn input2 = partialTransaction2.Inputs[i];

                if ((input1.PrevOut.N != input2.PrevOut.N) || (input1.PrevOut.Hash != input2.PrevOut.Hash))
                {
                    return false;
                }
            }

            for (int i = 0; i < partialTransaction1.Outputs.Count; i++)
            {
                TxOut output1 = partialTransaction1.Outputs[i];
                TxOut output2 = partialTransaction2.Outputs[i];

                if ((output1.Value != output2.Value) || (output1.ScriptPubKey != output2.ScriptPubKey))
                    return false;
            }

            return true;
        }
    }
}
