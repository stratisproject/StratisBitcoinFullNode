using System;
using System.Linq;

namespace NBitcoin.Policy
{
    public class TransactionPolicyError
    {
        public TransactionPolicyError()
            : this(null as string)
        {

        }

        private string _Message;
        public TransactionPolicyError(string message)
        {
            this._Message = message;
        }
        public override string ToString()
        {
            return this._Message;
        }
    }

    public class TransactionSizePolicyError : TransactionPolicyError
    {
        public TransactionSizePolicyError(int actualSize, int maximumSize)
            : base("Transaction's size is too high. Actual value is " + actualSize + ", but the maximum is " + maximumSize)
        {
            this._ActualSize = actualSize;
            this._MaximumSize = maximumSize;
        }
        private readonly int _ActualSize;
        public int ActualSize
        {
            get
            {
                return this._ActualSize;
            }
        }
        private readonly int _MaximumSize;
        public int MaximumSize
        {
            get
            {
                return this._MaximumSize;
            }
        }
    }
    public class FeeTooHighPolicyError : TransactionPolicyError
    {
        public FeeTooHighPolicyError(Money fees, Money max)
            : base("Fee too high, actual is " + fees.ToString() + ", policy maximum is " + max.ToString())
        {
            this._ExpectedMaxFee = max;
            this._Fee = fees;
        }

        private readonly Money _Fee;
        public Money Fee
        {
            get
            {
                return this._Fee;
            }
        }
        private readonly Money _ExpectedMaxFee;
        public Money ExpectedMaxFee
        {
            get
            {
                return this._ExpectedMaxFee;
            }
        }
    }

    public class DustPolicyError : TransactionPolicyError
    {
        public DustPolicyError(Money value, Money dust)
            : base("Dust output detected, output value is " + value.ToString() + ", policy minimum is " + dust.ToString())
        {
            this._Value = value;
            this._DustThreshold = dust;
        }

        private readonly Money _Value;
        public Money Value
        {
            get
            {
                return this._Value;
            }
        }

        private readonly Money _DustThreshold;
        public Money DustThreshold
        {
            get
            {
                return this._DustThreshold;
            }
        }
    }

    public class FeeTooLowPolicyError : TransactionPolicyError
    {
        public FeeTooLowPolicyError(Money fees, Money min)
            : base($"Fee of {fees} is too low. The policy minimum is {min}.")
        {
            this._ExpectedMinFee = min;
            this._Fee = fees;
        }

        private readonly Money _Fee;
        public Money Fee
        {
            get
            {
                return this._Fee;
            }
        }
        private readonly Money _ExpectedMinFee;
        public Money ExpectedMinFee
        {
            get
            {
                return this._ExpectedMinFee;
            }
        }
    }

    public class InputPolicyError : TransactionPolicyError
    {
        public InputPolicyError(string message, IndexedTxIn txIn)
            : base(message)
        {
            this._OutPoint = txIn.PrevOut;
            this._InputIndex = txIn.Index;
        }

        private readonly OutPoint _OutPoint;
        public OutPoint OutPoint
        {
            get
            {
                return this._OutPoint;
            }
        }

        private readonly uint _InputIndex;
        public uint InputIndex
        {
            get
            {
                return this._InputIndex;
            }
        }
    }

    public class DuplicateInputPolicyError : TransactionPolicyError
    {
        public DuplicateInputPolicyError(IndexedTxIn[] duplicated)
            : base("Duplicate input " + duplicated[0].PrevOut)
        {
            this._OutPoint = duplicated[0].PrevOut;
            this._InputIndices = duplicated.Select(d => d.Index).ToArray();
        }

        private readonly OutPoint _OutPoint;
        public OutPoint OutPoint
        {
            get
            {
                return this._OutPoint;
            }
        }
        private readonly uint[] _InputIndices;
        public uint[] InputIndices
        {
            get
            {
                return this._InputIndices;
            }
        }
    }

    public class OutputPolicyError : TransactionPolicyError
    {
        public OutputPolicyError(string message, int outputIndex) :
            base(message)
        {
            this._OutputIndex = outputIndex;
        }
        private readonly int _OutputIndex;
        public int OutputIndex
        {
            get
            {
                return this._OutputIndex;
            }
        }
    }
    public class CoinNotFoundPolicyError : InputPolicyError
    {
        private IndexedTxIn _TxIn;
        public CoinNotFoundPolicyError(IndexedTxIn txIn)
            : base("No coin matching " + txIn.PrevOut + " was found", txIn)
        {
            this._TxIn = txIn;
        }

        internal Exception AsException()
        {
            return new CoinNotFoundException(this._TxIn);
        }
    }

    public class ScriptPolicyError : InputPolicyError
    {
        public ScriptPolicyError(IndexedTxIn input, ScriptError error, ScriptVerify scriptVerify, Script scriptPubKey)
            : base("Script error on input " + input.Index + " (" + error + ")", input)
        {
            this._ScriptError = error;
            this._ScriptVerify = scriptVerify;
            this._ScriptPubKey = scriptPubKey;
        }


        private readonly ScriptError _ScriptError;
        public ScriptError ScriptError
        {
            get
            {
                return this._ScriptError;
            }
        }

        private readonly ScriptVerify _ScriptVerify;
        public ScriptVerify ScriptVerify
        {
            get
            {
                return this._ScriptVerify;
            }
        }

        private readonly Script _ScriptPubKey;
        public Script ScriptPubKey
        {
            get
            {
                return this._ScriptPubKey;
            }

        }
    }
    public interface ITransactionPolicy
    {
        /// <summary>
        /// Check if the given transaction violate the policy
        /// </summary>
        /// <param name="transaction">The transaction</param>
        /// <param name="spentCoins">The previous coins</param>
        /// <returns>Policy errors</returns>
        TransactionPolicyError[] Check(Transaction transaction, ICoin[] spentCoins);
    }
}