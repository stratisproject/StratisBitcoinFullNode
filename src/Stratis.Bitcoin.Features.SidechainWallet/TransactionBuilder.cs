using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.BouncyCastle.Security;
using NBitcoin.Policy;

namespace Stratis.Bitcoin.Features.SidechainWallet
{
    /// <inheritdoc />
    public class TransactionBuilder : NBitcoin.TransactionBuilder
    {
        public Transaction BuildCrossChainTransaction(TransactionBuildContext context, Network network)
        {
            return BuildCrossChainTransaction(context, network, SigHash.All);
        }

        public Transaction BuildCrossChainTransaction(TransactionBuildContext context, Network network, SigHash sigHash)
        {
            var buildingContext = this.CreateTransactionBuildingContext();
            context.Transaction = FromStandardTransactionToCrossChainTransaction(context, network);
           
            buildingContext.Finish();

            if(context.Sign)
            {
                this.SignTransactionInPlace(context.Transaction, sigHash);
            }
            return context.Transaction;
        }
         
        private Transaction FromStandardTransactionToCrossChainTransaction(TransactionBuildContext context, Network network)
        {
            var changeAddress = context.ChangeAddress;

            var txClone = context.Transaction.Clone();
            var initialOutput = txClone.Outputs
                .Where(o => o.ScriptPubKey.PaymentScript.GetScriptAddress(network).Hash != changeAddress.Pubkey.Hash)
                .Single(); //there should only be one recipient apart from the changeAddress
            var sidechainMultisig = GetMultisigForChain(context.SidechainIdentifier);
            if(sidechainMultisig.Length < 1) throw new ValidationException(string.Format("Multisig for sidechain {0} has no public key", context.SidechainIdentifier));
            var pay2Multisig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(sidechainMultisig.Length / 2 + 1, sidechainMultisig);
            
            var gatewayOutput = new TxOut(txClone.TotalOut, pay2Multisig);
            txClone.Outputs.Remove(initialOutput);
            txClone.Outputs.Add(gatewayOutput);

            //get the end target address
            var sidechainAddressHash = initialOutput.ScriptPubKey.GetDestinationAddress(network).ScriptPubKey.Hash;
            var newSidechainOutput = new TxOut(Money.Zero, new Script()
                + OpcodeType.OP_RETURN + Encoding.UTF8.GetBytes(sidechainAddressHash.ToString()));
            txClone.Outputs.Add(newSidechainOutput);

            return txClone;
        }

        /// <summary>
        /// TODO : This needs to become a call to a sidechain specific API which allows a sidechain multisig to be retrieved
        /// from its unique identifier (cf.  https://stratisplatformuk.visualstudio.com/Stratis%20Full%20Node%20Backend/_workitems/edit/965)
        /// </summary>
        /// <param name="sidechainIdentifier"></param>
        /// <returns></returns>
        public PubKey[] GetMultisigForChain(string sidechainIdentifier)
        {
            try
            {
                var randomMultisig = Enumerable.Range(0,5).Select(i => new PubKey($"a{i}")).ToArray();
                return randomMultisig;
            }
            // TODO : make the exception more specific once we know where it comes from
            catch (Exception exception)
            {
                throw new Exception(string.Format("unable to retrieve sidechain multisig for {0}", sidechainIdentifier), exception);
            }
        }

        public override bool Verify(Transaction tx, out TransactionPolicyError[] errors)
        {
            var isBaseVerified = base.Verify(tx, out errors);
            var sidechainErrors = new List<TransactionPolicyError>(errors);
            var sidechainVerifies = tx.Outputs.Count() == 3;
            if (!sidechainVerifies)
            {
                sidechainErrors.Add(new TransactionPolicyError("Cross chains transaction can only have 3 outputs"));
                return false;
            }
            var lastOutput = tx.Outputs.Last();
            sidechainVerifies = lastOutput.Value.Equals(Money.Zero);
            if (!sidechainVerifies)
            {
                sidechainErrors.Add(new TransactionPolicyError("Cross chains transaction must have an OP_RETURN output with an value of 0;"));
            }
            //TODO find how to verify that the last output is actually OP_RETURN
            return sidechainVerifies;
        }
    }
}
