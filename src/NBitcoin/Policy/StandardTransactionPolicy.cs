using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Policy
{
    public class StandardTransactionPolicy : ITransactionPolicy
    {
        private readonly Network network;

        public StandardTransactionPolicy(Network network)
        {
            this.network = network;
            this.ScriptVerify = NBitcoin.ScriptVerify.Standard;
            this.MaxTransactionSize = 100000;
            // TODO: replace fee params with whats in Network.
            this.MaxTxFee = new FeeRate(Money.Coins(0.1m));
            this.MinRelayTxFee = new FeeRate(Money.Satoshis(5000)); // TODO: new FeeRate(Money.Satoshis(network.MinRelayTxFee));
            this.CheckFee = true;
            this.CheckScriptPubKey = true;
        }

        public int? MaxTransactionSize
        {
            get;
            set;
        }
        /// <summary>
        /// Safety check, if the FeeRate exceed this value, a policy error is raised
        /// </summary>
        public FeeRate MaxTxFee
        {
            get;
            set;
        }
        public FeeRate MinRelayTxFee
        {
            get;
            set;
        }

        public ScriptVerify? ScriptVerify
        {
            get;
            set;
        }
        /// <summary>
        /// Check if the transaction is safe from malleability (default: false)
        /// </summary>
        public bool CheckMalleabilitySafe
        {
            get; set;
        } = false;
        public bool CheckFee
        {
            get;
            set;
        }
#if !NOCONSENSUSLIB
        public bool UseConsensusLib
        {
            get;
            set;
        }
#endif
        public const int MaxScriptSigLength = 1650;
        #region ITransactionPolicy Members

        public TransactionPolicyError[] Check(Transaction transaction, ICoin[] spentCoins)
        {
            if(transaction == null)
                throw new ArgumentNullException("transaction");

            spentCoins = spentCoins ?? new ICoin[0];

            var errors = new List<TransactionPolicyError>();



            foreach(IndexedTxIn input in transaction.Inputs.AsIndexedInputs())
            {
                ICoin coin = spentCoins.FirstOrDefault(s => s.Outpoint == input.PrevOut);
                if(coin != null)
                {
                    if(this.ScriptVerify != null)
                    {
                        ScriptError error;
                        if(!VerifyScript(input, coin.TxOut.ScriptPubKey, coin.TxOut.Value, this.ScriptVerify.Value, out error))
                        {
                            errors.Add(new ScriptPolicyError(input, error, this.ScriptVerify.Value, coin.TxOut.ScriptPubKey));
                        }
                    }
                }

                TxIn txin = input.TxIn;
                if(txin.ScriptSig.Length > MaxScriptSigLength)
                {
                    errors.Add(new InputPolicyError("Max scriptSig length exceeded actual is " + txin.ScriptSig.Length + ", max is " + MaxScriptSigLength, input));
                }
                if(!txin.ScriptSig.IsPushOnly)
                {
                    errors.Add(new InputPolicyError("All operation should be push", input));
                }
                if(!txin.ScriptSig.HasCanonicalPushes)
                {
                    errors.Add(new InputPolicyError("All operation should be canonical push", input));
                }
            }

            if(this.CheckMalleabilitySafe)
            {
                foreach(IndexedTxIn input in transaction.Inputs.AsIndexedInputs())
                {
                    ICoin coin = spentCoins.FirstOrDefault(s => s.Outpoint == input.PrevOut);
                    if(coin != null && coin.GetHashVersion(this.network) != HashVersion.Witness)
                        errors.Add(new InputPolicyError("Malleable input detected", input));
                }
            }

            if(this.CheckScriptPubKey)
            {
                foreach(Coin txout in transaction.Outputs.AsCoins())
                {
                    ScriptTemplate template = StandardScripts.GetTemplateFromScriptPubKey(this.network, txout.ScriptPubKey);
                    if(template == null)
                        errors.Add(new OutputPolicyError("Non-Standard scriptPubKey", (int)txout.Outpoint.N));
                }
            }

            int txSize = transaction.GetSerializedSize();
            if(this.MaxTransactionSize != null)
            {
                if(txSize >= this.MaxTransactionSize.Value)
                    errors.Add(new TransactionSizePolicyError(txSize, this.MaxTransactionSize.Value));
            }

            Money fees = transaction.GetFee(spentCoins);
            if(fees != null)
            {
                if(this.CheckFee)
                {
                    if(this.MaxTxFee != null)
                    {
                        Money max = this.MaxTxFee.GetFee(txSize);
                        if(fees > max)
                            errors.Add(new FeeTooHighPolicyError(fees, max));
                    }

                    if(this.MinRelayTxFee != null)
                    {
                        if(this.MinRelayTxFee != null)
                        {
                            Money min = this.MinRelayTxFee.GetFee(txSize);
                            if(fees < min)
                                errors.Add(new FeeTooLowPolicyError(fees, min));
                        }
                    }
                }
            }
            if(this.MinRelayTxFee != null)
            {
                foreach(TxOut output in transaction.Outputs)
                {
                    byte[] bytes = output.ScriptPubKey.ToBytes(true);
                    if(output.IsDust(this.MinRelayTxFee) && !IsOpReturn(bytes))
                        errors.Add(new DustPolicyError(output.Value, output.GetDustThreshold(this.MinRelayTxFee)));
                }
            }
            int opReturnCount = transaction.Outputs.Select(o => o.ScriptPubKey.ToBytes(true)).Count(b => IsOpReturn(b));
            if(opReturnCount > 1)
                errors.Add(new TransactionPolicyError("More than one op return detected"));
            return errors.ToArray();
        }

        private static bool IsOpReturn(byte[] bytes)
        {
            return bytes.Length > 0 && bytes[0] == (byte)OpcodeType.OP_RETURN;
        }

        private bool VerifyScript(IndexedTxIn input, Script scriptPubKey, Money value, ScriptVerify scriptVerify, out ScriptError error)
        {
#if !NOCONSENSUSLIB
            if(!this.UseConsensusLib)
#endif
                return input.VerifyScript(this.network, scriptPubKey, value, scriptVerify, out error);
#if !NOCONSENSUSLIB
            else
            {
                bool ok = Script.VerifyScriptConsensus(scriptPubKey, input.Transaction, input.Index, scriptVerify);
                if(!ok)
                {
                    if(input.VerifyScript(this.network, scriptPubKey, scriptVerify, out error))
                        error = ScriptError.UnknownError;
                    return false;
                }
                else
                {
                    error = ScriptError.OK;
                }
                return true;
            }
#endif
        }

        #endregion

        public StandardTransactionPolicy Clone()
        {
            return new StandardTransactionPolicy(this.network)
            {
                MaxTransactionSize = this.MaxTransactionSize,
                MaxTxFee = this.MaxTxFee,
                MinRelayTxFee = this.MinRelayTxFee,
                ScriptVerify = this.ScriptVerify,
#if !NOCONSENSUSLIB
                UseConsensusLib = this.UseConsensusLib,
#endif
                CheckMalleabilitySafe = this.CheckMalleabilitySafe,
                CheckScriptPubKey = this.CheckScriptPubKey,
                CheckFee = this.CheckFee
            };
        }

        /// <summary>
        /// Check the standardness of scriptPubKey
        /// </summary>
        public bool CheckScriptPubKey
        {
            get;
            set;
        }
    }
}
