using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public static class SmartContractConsensusErrors
    {
        public static readonly ConsensusError UserOpSpend = new ConsensusError("op-spend-by-user", "op spend opcode invoked by a user");
        public static readonly ConsensusError UserInternalCall = new ConsensusError("op-spend-by-user", "op internalcall opcode invoked by a user");
        public static readonly ConsensusError UnequalCondensingTx = new ConsensusError("invalid-condensing-tx", "condensing tx generated didn't match tx in block");
        public static readonly ConsensusError UnequalStateRoots = new ConsensusError("invalid-state-roots", "contract state root not matching after block execution");
        public static readonly ConsensusError UnequalReceiptRoots = new ConsensusError("invalid-receipt-roots", "contract receipt roots not matching after block execution");
        public static readonly ConsensusError UnequalRefundAmounts = new ConsensusError("invalid-refund-amount", "contract execution refunded a different amount or to a different address");
        public static readonly ConsensusError FeeTooSmallForGas = new ConsensusError("total-gas-value-greater-than-total-fee", "total supplied gas value was greater than total supplied fee value");
    }
}