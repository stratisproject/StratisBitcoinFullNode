using NBitcoin;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class Receipt
    {
        public uint256 TransactionId { get; }

        public uint256 BlockHash { get; }

        public uint160 Sender { get; }

        public uint160 To { get; }

        public uint160 NewContractAddress { get; }

        public ulong GasUsed { get; }

        public bool Success { get; }

        public byte[] ReturnValue { get; }

        public Receipt(
            uint256 transactionId,
            uint256 blockHash,
            uint160 sender,
            uint160 to,
            uint160 newContractAddress,
            ulong gasUsed,
            bool success,
            byte[] returnValue)
        {
            this.TransactionId = transactionId;
            this.BlockHash = blockHash;
            this.Sender = sender;
            this.To = to;
            this.NewContractAddress = newContractAddress;
            this.GasUsed = gasUsed;
            this.Success = success;
            this.ReturnValue = returnValue;
        }
    }
}
