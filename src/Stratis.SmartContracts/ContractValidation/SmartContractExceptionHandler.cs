using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.Exceptions;

namespace Stratis.SmartContracts.ContractValidation
{
    public sealed class SmartContractExceptionHandler
    {
        private readonly List<HandleContractException> handlers;

        public SmartContractExceptionHandler(Transaction coinbase)
        {
            this.handlers = new List<HandleContractException>
            {
                new HandleOutOfGasException(coinbase),
                new HandleRefundGasException(coinbase)
            };
        }

        public void Process(SmartContractCarrier carrier, SmartContractExecutionResult result)
        {
            HandleContractException handler = this.handlers.FirstOrDefault(p => p.GetType() == result.Exception.GetType());
            if (handler != null)
                handler.OnProcess(carrier, result);
        }
    }

    public abstract class HandleContractException
    {
        public abstract Type ApplicableException { get; }
        public readonly Transaction Coinbase;
        public abstract void OnProcess(SmartContractCarrier carrier, SmartContractExecutionResult result);

        protected HandleContractException(Transaction coinbase)
        {
            this.Coinbase = coinbase;
        }
    }

    public sealed class HandleOutOfGasException : HandleContractException
    {
        public override Type ApplicableException
        {
            get { return typeof(OutOfGasException); }
        }

        public HandleOutOfGasException(Transaction coinbase)
            : base(coinbase)
        {
        }

        public override void OnProcess(SmartContractCarrier carrier, SmartContractExecutionResult result)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class HandleRefundGasException : HandleContractException
    {
        public override Type ApplicableException
        {
            get { return typeof(RefundGasException); }
        }

        public HandleRefundGasException(Transaction coinbase)
            : base(coinbase)
        {
        }

        public override void OnProcess(SmartContractCarrier carrier, SmartContractExecutionResult result)
        {
            var spent = carrier.GasUnitPrice * result.GasUnitsUsed;
            var refund = carrier.GasCostBudget - spent;
            //var txFeeAndGas = mempoolEntry.Fee - refund;

            //Is any of this correct in this scenario??
            //Add spent fees to the block----------------------------
            //this.pblocktemplate.VTxFees[0] = -this.fees;
            //this.coinbase.Outputs[0].Value = this.fees + this.consensusLoop.Validator.GetProofOfWorkReward(this.height);
            //this.pblocktemplate.TotalFee = this.fees;
            //-------------------------------------------------------

            //Refund the caller--------------------------------------
            var refundScript = new Script(
                OpcodeType.OP_DUP,
                OpcodeType.OP_HASH160,
                Op.GetPushOp(carrier.Sender.ToBytes()),
                OpcodeType.OP_EQUALVERIFY,
                OpcodeType.OP_CHECKSIG
            );

            this.Coinbase.Outputs.Add(new TxOut(spent, refundScript));
            //-------------------------------------------------------
        }
    }
}