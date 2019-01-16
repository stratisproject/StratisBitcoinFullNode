using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public static class SmartContractConsensusErrors
    {
        public static ConsensusError UserOpSpend => new ConsensusError("op-spend-by-user", "op spend opcode invoked by a user");
        public static ConsensusError UserInternalCall => new ConsensusError("op-spend-by-user", "op internalcall opcode invoked by a user");
        public static ConsensusError UnequalCondensingTx => new ConsensusError("invalid-condensing-tx", "condensing tx generated didn't match tx in block");
        public static ConsensusError UnequalStateRoots => new ConsensusError("invalid-state-roots", "contract state root not matching after block execution");
        public static ConsensusError UnequalReceiptRoots => new ConsensusError("invalid-receipt-roots", "contract receipt roots not matching after block execution");
        public static ConsensusError UnequalRefundAmounts => new ConsensusError("invalid-refund-amount", "contract execution refunded a different amount or to a different address");
        public static ConsensusError FeeTooSmallForGas => new ConsensusError("total-gas-value-greater-than-total-fee", "total supplied gas value was greater than total supplied fee value");
    }
}