using System.Linq;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class ReceiptResponse
    {
        public string TransactionHash { get; }
        public string BlockHash { get; }
        public string PostState { get; }
        public ulong GasUsed { get; }
        public string From { get; }
        public string To { get; }
        public string NewContractAddress { get; }
        public bool Success { get; }
        public string Bloom { get; }
        public LogResponse[] Logs { get; }
        public ReceiptResponse(Receipt receipt)
        {
            this.TransactionHash = receipt.TransactionHash.ToString();
            this.BlockHash = receipt.BlockHash.ToString();
            this.PostState = receipt.PostState.ToString();
            this.GasUsed = receipt.GasUsed;
            this.From = receipt.From.ToString();
            this.To = receipt.To?.ToString();
            this.NewContractAddress = receipt.NewContractAddress?.ToString();
            this.Success = receipt.Success;
            this.Bloom = receipt.Bloom.ToString();
            this.Logs = receipt.Logs.Select(x => new LogResponse(x)).ToArray();
        }
    }

    public class LogResponse
    {
        public string Address { get; }
        public string[] Topics { get; }
        public string Data { get; }

        public LogResponse(Log log)
        {
            this.Address = log.Address.ToString();
            this.Topics = log.Topics.Select(x => x.ToHexString()).ToArray();
            this.Data = log.Data.ToHexString();
        }
    }
}
