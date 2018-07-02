using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class ReceiptModel
    {
        public string TxHash { get; set; }

        public ulong BlockHeight { get; set; }

        public string ContractAddress { get; set; }

        public bool Successful { get; set; }

        public string Exception { get; set; }

        public string Returned { get; set; }

        public ReceiptModel() { }

        public static ReceiptModel FromSmartContractReceipt(SmartContractReceipt receipt, Network network)
        {
            return new ReceiptModel
            {
                TxHash = new uint256(receipt.TxHash).ToString(),
                BlockHeight = receipt.BlockHeight,
                ContractAddress = receipt.NewContractAddress != null ? new uint160(receipt.NewContractAddress).ToAddress(network).ToString() : null,
                Successful = receipt.Successful,
                Exception = receipt.Exception,
                Returned = receipt.Returned
            };
        }
    }
}