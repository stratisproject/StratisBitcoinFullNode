using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Stealth
{
    public class StealthSpendKey
    {
        private readonly StealthPayment _Payment;
        public StealthPayment Payment
        {
            get
            {
                return this._Payment;
            }
        }
        private readonly KeyId _ID;
        public KeyId ID
        {
            get
            {
                return this._ID;
            }
        }
        public StealthSpendKey(KeyId id, StealthPayment payment)
        {
            this._ID = id;
            this._Payment = payment;
        }

        public BitcoinAddress GetAddress(Network network)
        {
            return new BitcoinPubKeyAddress(this.ID, network);
        }
    }

    public class StealthPayment
    {
        public StealthPayment(BitcoinStealthAddress address, Key ephemKey, StealthMetadata metadata)
        {
            this.Metadata = metadata;
            this.ScriptPubKey = CreatePaymentScript(address.SignatureCount, address.SpendPubKeys, ephemKey, address.ScanPubKey);

            if(address.SignatureCount > 1)
            {
                this.Redeem = this.ScriptPubKey;
                this.ScriptPubKey = this.ScriptPubKey.Hash.ScriptPubKey;
            }
            SetStealthKeys();
        }

        public static Script CreatePaymentScript(int sigCount, PubKey[] spendPubKeys, Key ephemKey, PubKey scanPubKey)
        {
            return CreatePaymentScript(sigCount, spendPubKeys.Select(p => p.Uncover(ephemKey, scanPubKey)).ToArray());
        }

        public static Script CreatePaymentScript(int sigCount, PubKey[] uncoveredPubKeys)
        {
            if(sigCount == 1 && uncoveredPubKeys.Length == 1)
            {
                return PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(uncoveredPubKeys[0].Hash);
            }
            else
            {
                return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(sigCount, uncoveredPubKeys);
            }
        }

        public static Script CreatePaymentScript(BitcoinStealthAddress address, PubKey ephemKey, Key scan)
        {
            return CreatePaymentScript(address.SignatureCount, address.SpendPubKeys.Select(p => p.UncoverReceiver(scan, ephemKey)).ToArray());
        }

        public static KeyId[] ExtractKeyIDs(Script script)
        {
            KeyId keyId = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(script);
            if(keyId != null)
            {
                return new[] { keyId };
            }
            else
            {
                PayToMultiSigTemplateParameters para = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(script);
                if(para == null)
                    throw new ArgumentException("Invalid stealth spendable output script", "spendable");
                return para.PubKeys.Select(k => k.Hash).ToArray();
            }
        }

        public StealthSpendKey[] StealthKeys
        {
            get;
            private set;
        }

        public BitcoinAddress[] GetAddresses(Network network)
        {
            return this.StealthKeys.Select(k => k.GetAddress(network)).ToArray();
        }

        public StealthPayment(Script scriptPubKey, Script redeem, StealthMetadata metadata)
        {
            this.Metadata = metadata;
            this.ScriptPubKey = scriptPubKey;
            this.Redeem = redeem;
            SetStealthKeys();
        }

        private void SetStealthKeys()
        {
            this.StealthKeys = ExtractKeyIDs(this.Redeem ?? this.ScriptPubKey).Select(id => new StealthSpendKey(id, this)).ToArray();
        }


        public StealthMetadata Metadata
        {
            get;
            private set;
        }
        public Script ScriptPubKey
        {
            get;
            private set;
        }
        public Script Redeem
        {
            get;
            private set;
        }

        public void AddToTransaction(Transaction transaction, Money value)
        {
            if(transaction == null)
                throw new ArgumentNullException("transaction");
            if(value == null)
                throw new ArgumentNullException("value");
            transaction.Outputs.Add(new TxOut(0, this.Metadata.Script));
            transaction.Outputs.Add(new TxOut(value, this.ScriptPubKey));
        }

        public static StealthPayment[] GetPayments(Transaction transaction, BitcoinStealthAddress address, Key scan)
        {
            var result = new List<StealthPayment>();
            for(int i = 0; i < transaction.Outputs.Count - 1; i++)
            {
                StealthMetadata metadata = StealthMetadata.TryParse(transaction.Outputs[i].ScriptPubKey);
                if(metadata != null && (address == null || address.Prefix.Match(metadata.BitField)))
                {
                    Script scriptPubKey = transaction.Outputs[i + 1].ScriptPubKey;
                    ScriptId scriptId = PayToScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey);
                    Script expectedScriptPubKey = address == null ? scriptPubKey : null;
                    Script redeem = null;

                    if(scriptId != null)
                    {
                        if(address == null)
                            throw new ArgumentNullException("address");
                        redeem = CreatePaymentScript(address, metadata.EphemKey, scan);
                        expectedScriptPubKey = redeem.Hash.ScriptPubKey;
                        if(expectedScriptPubKey != scriptPubKey)
                            continue;
                    }

                    var payment = new StealthPayment(scriptPubKey, redeem, metadata);
                    if(scan != null)
                    {
                        if(address != null && payment.StealthKeys.Length != address.SpendPubKeys.Length)
                            continue;

                        if(expectedScriptPubKey == null)
                        {
                            expectedScriptPubKey = CreatePaymentScript(address, metadata.EphemKey, scan);
                        }

                        if(expectedScriptPubKey != scriptPubKey)
                            continue;
                    }
                    result.Add(payment);
                }
            }
            return result.ToArray();
        }


    }
}
