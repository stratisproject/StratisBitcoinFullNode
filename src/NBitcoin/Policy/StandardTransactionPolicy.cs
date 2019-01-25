using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Policy
{
    public class StandardTransactionPolicy : ITransactionPolicy
    {
        /// <summary>
        /// Biggest 'standard' txin is a 15-of-15 P2SH multisig with compressed keys
        /// (remember the 520 byte limit on redeemScript size).
        /// That works out to a (15*(33+1))+3=513 byte redeemScript, 513+1+15*(73+1)+3=1627 bytes of scriptSig, 
        /// which we round off to 1650 bytes for some minor future-proofing. 
        /// That's also enough to spend a 20-of-20 CHECKMULTISIG scriptPubKey, though such a scriptPubKey is not considered standard.
        /// </summary>
        public const int MaxScriptSigLength = 1650;

        /// <summary>
        /// The maximum size for transactions we're willing to relay/mine.
        /// </summary>
        public int? MaxTransactionSize { get; set; }

        /// <summary>
        /// Safety check, if the FeeRate exceed this value, a policy error is raised.
        /// </summary>
        public FeeRate MaxTxFee { get; set; }

        /// <summary>
        /// Fees smaller than this (in satoshi) are considered zero fee (for relaying).
        /// </summary>
        public FeeRate MinRelayTxFee { get; set; }

        public ScriptVerify? ScriptVerify { get; set; }

        /// <summary>
        /// Check if the transaction is safe from malleability (default: false).
        /// </summary>
        public bool CheckMalleabilitySafe { get; set; }

        /// <summary>
        /// A value indicating whether to include checking the fee as part of checking the transaction.
        /// This is set to false in some unit tests but otherwise defaults to true.
        /// </summary>
        public bool CheckFee { get; set; }

        /// <summary>
        /// Check the standardness of scriptPubKey.
        /// </summary>
        public bool CheckScriptPubKey { get; set; }

        private readonly Network network;

        public StandardTransactionPolicy(Network network)
        {
            this.network = network;
            this.ScriptVerify = NBitcoin.ScriptVerify.Standard;
            this.MaxTransactionSize = 100000;
            // TODO: replace fee params with whats in Network.
            this.MaxTxFee = new FeeRate(Money.Coins(0.1m));
            this.MinRelayTxFee = new FeeRate(Money.Satoshis(network.MinRelayTxFee));
            this.CheckFee = true;
            this.CheckScriptPubKey = true;
        }

        public TransactionPolicyError[] Check(Transaction transaction, ICoin[] spentCoins)
        {
            if (transaction == null)
                throw new ArgumentNullException("transaction");

            spentCoins = spentCoins ?? new ICoin[0];

            var errors = new List<TransactionPolicyError>();

            foreach (IndexedTxIn input in transaction.Inputs.AsIndexedInputs())
            {
                ICoin coin = spentCoins.FirstOrDefault(s => s.Outpoint == input.PrevOut);
                if (coin != null)
                {
                    if (this.ScriptVerify != null)
                    {
                        if(!input.VerifyScript(this.network, coin.TxOut.ScriptPubKey, coin.TxOut.Value, this.ScriptVerify.Value, out ScriptError error))
                        {
                            errors.Add(new ScriptPolicyError(input, error, this.ScriptVerify.Value, coin.TxOut.ScriptPubKey));
                        }
                    }
                }

                TxIn txin = input.TxIn;
                if (txin.ScriptSig.Length > MaxScriptSigLength)
                {
                    errors.Add(new InputPolicyError("Max scriptSig length exceeded actual is " + txin.ScriptSig.Length + ", max is " + MaxScriptSigLength, input));
                }
                if (!txin.ScriptSig.IsPushOnly)
                {
                    errors.Add(new InputPolicyError("All operation should be push", input));
                }
                if (!txin.ScriptSig.HasCanonicalPushes)
                {
                    errors.Add(new InputPolicyError("All operation should be canonical push", input));
                }
            }

            if (this.CheckMalleabilitySafe)
            {
                foreach (IndexedTxIn input in transaction.Inputs.AsIndexedInputs())
                {
                    ICoin coin = spentCoins.FirstOrDefault(s => s.Outpoint == input.PrevOut);
                    if (coin != null && coin.GetHashVersion(this.network) != HashVersion.Witness)
                        errors.Add(new InputPolicyError("Malleable input detected", input));
                }
            }

            CheckPubKey(transaction, errors);

            int txSize = transaction.GetSerializedSize();
            if (this.MaxTransactionSize != null)
            {
                if (txSize >= this.MaxTransactionSize.Value)
                    errors.Add(new TransactionSizePolicyError(txSize, this.MaxTransactionSize.Value));
            }

            Money fees = transaction.GetFee(spentCoins);
            if (fees != null)
            {
                if (this.CheckFee)
                {
                    if (this.MaxTxFee != null)
                    {
                        Money max = this.MaxTxFee.GetFee(txSize);
                        if (fees > max)
                            errors.Add(new FeeTooHighPolicyError(fees, max));
                    }

                    if (this.MinRelayTxFee != null)
                    {
                        if (this.MinRelayTxFee != null)
                        {
                            Money min = this.MinRelayTxFee.GetFee(txSize);
                            if (fees < min)
                                errors.Add(new FeeTooLowPolicyError(fees, min));
                        }
                    }
                }
            }

            this.CheckMinRelayTxFee(transaction, errors);

            int opReturnCount = transaction.Outputs.Select(o => o.ScriptPubKey.ToBytes(true)).Count(b => IsOpReturn(b));
            if (opReturnCount > 1)
                errors.Add(new TransactionPolicyError("More than one op return detected"));

            return errors.ToArray();
        }

        protected virtual void CheckPubKey(Transaction transaction, List<TransactionPolicyError> errors)
        {
            if (this.CheckScriptPubKey)
            {
                foreach (Coin txout in transaction.Outputs.AsCoins())
                {
                    ScriptTemplate template = this.network.StandardScriptsRegistry.GetTemplateFromScriptPubKey(txout.ScriptPubKey);

                    if (template == null)
                        errors.Add(new OutputPolicyError("Non-Standard scriptPubKey", (int)txout.Outpoint.N));
                }
            }
        }

        protected virtual void CheckMinRelayTxFee(Transaction transaction, List<TransactionPolicyError> errors)
        {
            if (this.MinRelayTxFee != null)
            {
                foreach (TxOut output in transaction.Outputs)
                {
                    byte[] bytes = output.ScriptPubKey.ToBytes(true);

                    if (output.IsDust(this.MinRelayTxFee) && !IsOpReturn(bytes))
                        errors.Add(new DustPolicyError(output.Value, output.GetDustThreshold(this.MinRelayTxFee)));
                }
            }
        }

        protected static bool IsOpReturn(byte[] bytes)
        {
            return bytes.Length > 0 && bytes[0] == (byte)OpcodeType.OP_RETURN;
        }
        
        public StandardTransactionPolicy Clone()
        {
            return new StandardTransactionPolicy(this.network)
            {
                MaxTransactionSize = this.MaxTransactionSize,
                MaxTxFee = this.MaxTxFee,
                MinRelayTxFee = this.MinRelayTxFee,
                ScriptVerify = this.ScriptVerify,
                CheckMalleabilitySafe = this.CheckMalleabilitySafe,
                CheckScriptPubKey = this.CheckScriptPubKey,
                CheckFee = this.CheckFee
            };
        }
    }
}