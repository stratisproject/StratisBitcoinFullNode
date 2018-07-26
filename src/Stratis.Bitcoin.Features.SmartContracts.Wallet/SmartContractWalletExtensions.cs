using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public static class SmartContractWalletExtensions
    {
        // TODO Remove this code duplication
        public enum WalletScOpcodeType : byte
        {
            // smart contracts
            OP_CREATECONTRACT = 0xc0,
            OP_CALLCONTRACT = 0xc1
        }

        public static bool IsSmartContractExec(this Script script)
        {
            Op firstOp = script.ToOps().FirstOrDefault();

            if (firstOp == null)
                return false;

            var opCode = (byte)firstOp.Code;
            return opCode == (byte)WalletScOpcodeType.OP_CALLCONTRACT || opCode == (byte)WalletScOpcodeType.OP_CREATECONTRACT;
        }
    }
}