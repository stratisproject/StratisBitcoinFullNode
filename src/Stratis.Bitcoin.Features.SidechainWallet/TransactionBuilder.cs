using System;
using System.Collections.Generic;
using System.Linq;
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

            //get the end target address
            var sidechainAddressHash = sidechainOutput.ScriptPubKey.Hash.ToString().ToHexString();
            //replace the address with the multisig
            //TODO: need to add the value too somehow this is probably completely wrong
            var hexEncoder = new NBitcoin.DataEncoders.HexEncoder();
            var replacementOutput = new TxOut(txClone.TotalOut, new Script() + OpcodeType.OP_CHECKMULTISIG + hexEncoder.DecodeData(sidechainMultisig));
            txClone.Outputs.Remove(sidechainOutput);
            txClone.Outputs.Add(replacementOutput);

            txClone.Outputs.Add(new TxOut(Money.Zero, new Script() + OpcodeType.OP_RETURN + hexEncoder.DecodeData(sidechainAddressHash)));

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

        //private void CreateTxWithCustomOpReturn(string data)
        //{
        //    var hexString = data.ToHexString();
        //    Transaction tx = new Transaction();

        //    tx.Outputs.Add(new TxOut(Money.Zero, new Script() + OpcodeType.OP_RETURN + Encoders.Hex.DecodeData(hexString)))


        //    //var randomPubKey = BitcoinAddress.Create(hexString, this.network).ScriptPubKey;
        //    //tx.Outputs.Add(new TxOut(new Money(10000), randomPubKey));

        //    //Normal pub key:
        //    //tx.Outputs.Add(new TxOut(new Money(10000), new Script() + OpcodeType.OP_DUP + OpcodeType.OP_HASH160 + Encoders.Hex.DecodeData(hexString) + OpcodeType.OP_EQUALVERIFY + OpcodeType.OP_CHECKSIG));


        //    //tx.Outputs.Add(new TxOut(new Money(10000), new Script() + OpcodeType.OP_DUP + OpcodeType.OP_HASH160 + Encoding.UTF8.GetBytes(data) + OpcodeType.OP_EQUALVERIFY + OpcodeType.OP_CHECKSIG));


        //    new TransactionBuilder()
        //        .AddKeys(key)
        //        .AddCoins(new Coin(utxo.ToOutPoint(), new TxOut(utxo.Transaction.Amount, utxo.Transaction.ScriptPubKey)))
        //        .SignTransactionInPlace(tx);

        //    this.logger.LogInformation(tx.ToString());

        //    this.broadcasterManager.BroadcastTransactionAsync(tx).GetAwaiter().GetResult();

        //    string id = tx.GetHash().ToString();

        //    this.logger.LogInformation(id + " tx created");
        //}

        //public override bool Verify(Transaction tx, out TransactionPolicyError[] errors)
        //{
        //    var isBaseVerified = base.Verify(tx, out errors);
        //    var sidechainErrors = new List<TransactionPolicyError>(errors);
        //    var sidechainVerifies = tx.Outputs.Count() == 3;
        //    if (!sidechainVerifies)
        //    {
        //        sidechainErrors.Add(new TransactionPolicyError("Cross chains transaction can only have 3 outputs"));
        //        return false;
        //    }
        //    var lastOutput = tx.Outputs.Last(); 
        //    sidechainVerifies = lastOutput.Value.Equals(Money.Zero);
        //    if (!sidechainVerifies)
        //    {
        //        sidechainErrors.Add(new TransactionPolicyError("Cross chains transaction must have an OP_RETURN output with an value of 0;"));
        //    }
        //    //TODO find how to verify that the last output is actually OP_RETURN
        //    return sidechainVerifies;
        //}
    }
}
