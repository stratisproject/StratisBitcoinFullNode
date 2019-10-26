using System;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    public class TransactionSignature
    {
        private static readonly TransactionSignature _Empty = new TransactionSignature(new ECDSASignature(BouncyCastle.Math.BigInteger.ValueOf(0), BouncyCastle.Math.BigInteger.ValueOf(0)), SigHash.All);
        public static TransactionSignature Empty
        {
            get
            {
                return _Empty;
            }
        }

        /// <summary>
        /// Check if valid transaction signature
        /// </summary>
        /// <param name="network">The blockchain network class.</param>
        /// <param name="sig">Signature in bytes</param>
        /// <param name="scriptVerify">Verification rules</param>
        /// <returns>True if valid</returns>
        public static bool IsValid(Network network, byte[] sig, ScriptVerify scriptVerify = ScriptVerify.DerSig | ScriptVerify.StrictEnc)
        {
            ScriptError error;
            return IsValid(network, sig, scriptVerify, out error);
        }


        /// <summary>
        /// Check if valid transaction signature
        /// </summary>
        /// <param name="network">The blockchain network class.</param>
        /// <param name="sig">The signature</param>
        /// <param name="scriptVerify">Verification rules</param>
        /// <param name="error">Error</param>
        /// <returns>True if valid</returns>
        public static bool IsValid(Network network, byte[] sig, ScriptVerify scriptVerify, out ScriptError error)
        {
            if(sig == null)
                throw new ArgumentNullException("sig");
            if(sig.Length == 0)
            {
                error = ScriptError.SigDer;
                return false;
            }
            error = ScriptError.OK;
            var ctx = new ScriptEvaluationContext(network)
            {
                ScriptVerify = scriptVerify
            };
            if(!ctx.CheckSignatureEncoding(sig))
            {
                error = ctx.Error;
                return false;
            }
            return true;
        }
        public TransactionSignature(ECDSASignature signature, SigHash sigHash)
        {
            if(sigHash == SigHash.Undefined)
                throw new ArgumentException("sigHash should not be Undefined");
            this._SigHash = sigHash;
            this._Signature = signature;
        }
        public TransactionSignature(ECDSASignature signature)
            : this(signature, SigHash.All)
        {

        }
        public TransactionSignature(byte[] sigSigHash)
        {
            this._Signature = ECDSASignature.FromDER(sigSigHash);
            this._SigHash = (SigHash)sigSigHash[sigSigHash.Length - 1];
        }
        public TransactionSignature(byte[] sig, SigHash sigHash)
        {
            this._Signature = ECDSASignature.FromDER(sig);
            this._SigHash = sigHash;
        }

        private readonly ECDSASignature _Signature;
        public ECDSASignature Signature
        {
            get
            {
                return this._Signature;
            }
        }
        private readonly SigHash _SigHash;
        public SigHash SigHash
        {
            get
            {
                return this._SigHash;
            }
        }

        public byte[] ToBytes()
        {
            byte[] sig = this._Signature.ToDER();
            var result = new byte[sig.Length + 1];
            Array.Copy(sig, 0, result, 0, sig.Length);
            result[result.Length - 1] = (byte) this._SigHash;
            return result;
        }

        public static bool ValidLength(int length)
        {
            return (67 <= length && length <= 80) || length == 9; //9 = Empty signature
        }

        public bool Check(Network network, PubKey pubKey, Script scriptPubKey, IndexedTxIn txIn, ScriptVerify verify = ScriptVerify.Standard)
        {
            return Check(network, pubKey, scriptPubKey, txIn.Transaction, txIn.Index, verify);
        }

        public bool Check(Network network, PubKey pubKey, Script scriptPubKey, Transaction tx, uint nIndex, ScriptVerify verify = ScriptVerify.Standard)
        {
            return new ScriptEvaluationContext(network)
            {
                ScriptVerify = verify,
                SigHash = this.SigHash
            }.CheckSig(this, pubKey, scriptPubKey, tx, nIndex);
        }

        private string _Id;
        private string Id
        {
            get
            {
                if(this._Id == null) this._Id = Encoders.Hex.EncodeData(ToBytes());
                return this._Id;
            }
        }

        public override bool Equals(object obj)
        {
            var item = obj as TransactionSignature;
            if(item == null)
                return false;
            return this.Id.Equals(item.Id);
        }
        public static bool operator ==(TransactionSignature a, TransactionSignature b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return a.Id == b.Id;
        }

        public static bool operator !=(TransactionSignature a, TransactionSignature b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public override string ToString()
        {
            return Encoders.Hex.EncodeData(ToBytes());
        }

        public bool IsLowS
        {
            get
            {
                return this.Signature.IsLowS;
            }
        }


        /// <summary>
        /// Enforce LowS on the signature
        /// </summary>
        public TransactionSignature MakeCanonical()
        {
            if(this.IsLowS)
                return this;
            return new TransactionSignature(this.Signature.MakeCanonical(), this.SigHash);
        }
    }
}
