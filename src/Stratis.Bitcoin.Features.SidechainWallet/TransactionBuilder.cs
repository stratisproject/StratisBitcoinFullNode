using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
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
            context.Transaction = FromStandardTransactionToCrossChainTransaction(buildingContext, context, network);
           
            buildingContext.Finish();

            if(context.Sign)
            {
                SignTransactionInPlace(context.Transaction, sigHash);
            }
            return context.Transaction;
        }
         
        private Transaction FromStandardTransactionToCrossChainTransaction(TransactionBuildingContext buildingContext, TransactionBuildContext context, Network network)
        {
            var sidechainMultisig = GetMultisigForChain(context.SidechainIdentifier);
            var changeAddress = context.ChangeAddress;

            var txClone = context.Transaction.Clone();
            var sidechainOutput = txClone.Outputs
                .Where(o => o.ScriptPubKey.PaymentScript.GetScriptAddress(network).Hash != changeAddress.Pubkey.Hash)
                .Single(); //there should only be one recipient apart from the changeAddress

            var gatewayOutput = new TxOut(txClone.TotalOut, new Script()
                + OpcodeType.OP_DUP + OpcodeType.OP_HASH160 + Encoding.UTF8.GetBytes(sidechainMultisig) + OpcodeType.OP_EQUALVERIFY + OpcodeType.OP_CHECKSIG);
            txClone.Outputs.Remove(sidechainOutput);
            txClone.Outputs.Add(gatewayOutput);

            //get the end target address
            var sidechainAddressHash = sidechainOutput.ScriptPubKey.Hash.ToString().ToHexString();
            var newSidechainOutput = new TxOut(Money.Zero, new Script()
                + OpcodeType.OP_RETURN + Encoding.UTF8.GetBytes(sidechainAddressHash));
            txClone.Outputs.Add(newSidechainOutput);

            return txClone;
        }

        /// <summary>
        /// TODO : This needs to become a call to a sidechain specific API which allows a sidechain multisig to be retrieved
        /// from its unique identifier (cf.  https://stratisplatformuk.visualstudio.com/Stratis%20Full%20Node%20Backend/_workitems/edit/965)
        /// </summary>
        /// <param name="sidechainIdentifier"></param>
        /// <returns></returns>
        public string GetMultisigForChain(string sidechainIdentifier)
        {
            try
            {
                return "sladkjjldsfkajsdajkflajdlfl";
            }
            catch (Exception exception)
            {
                throw new Exception(string.Format("unable to retrieve sidechain multisig for {0}", sidechainIdentifier));
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
