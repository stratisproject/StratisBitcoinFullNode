using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public static class SmartContractScript
    {
        public static bool IsSmartContractExec(this Script script)
        {
            Op firstOp = script.ToOps().FirstOrDefault();

            if (firstOp == null)
                return false;

            var opCode = (byte)firstOp.Code;

            return opCode == (byte)ScOpcodeType.OP_CALLCONTRACT || opCode == (byte)ScOpcodeType.OP_CREATECONTRACT;
        }

        public static bool IsSmartContractCall(this Script script)
        {
            Op firstOp = script.ToOps().FirstOrDefault();

            if (firstOp == null)
                return false;

            return (byte)firstOp.Code == (byte)ScOpcodeType.OP_CALLCONTRACT;
        }

        public static bool IsSmartContractCreate(this Script script)
        {
            Op firstOp = script.ToOps().FirstOrDefault();

            if (firstOp == null)
                return false;

            return (byte)firstOp.Code == (byte)ScOpcodeType.OP_CREATECONTRACT;
        }

        public static bool IsSmartContractSpend(this Script script)
        {
            Op op = script.ToOps().FirstOrDefault();
            if (op == null)
                return false;

            return (byte)op.Code == (byte)ScOpcodeType.OP_SPEND;
        }

        public static bool IsSmartContractInternalCall(this Script script)
        {
            var op = script.ToOps().FirstOrDefault();
            if (op == null)
                return false;

            return (byte)op.Code == (byte)ScOpcodeType.OP_INTERNALCONTRACTTRANSFER;
        }
    }

    public enum ScOpcodeType : byte
    {
        // smart contracts
        OP_CREATECONTRACT = 0xc0,
        OP_CALLCONTRACT = 0xc1,
        OP_SPEND = 0xc2,
        OP_INTERNALCONTRACTTRANSFER = 0xc3
    }
}