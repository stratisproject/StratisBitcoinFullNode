using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.CLR;
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
        public string ReturnValue { get; }
        public string Bloom { get; }
        public string Error { get; }
        public LogResponse[] Logs { get; }
        public ReceiptResponse(Receipt receipt, Network network)
        {
            this.TransactionHash = receipt.TransactionHash.ToString();
            this.BlockHash = receipt.BlockHash.ToString();
            this.PostState = receipt.PostState.ToString();
            this.GasUsed = receipt.GasUsed;
            this.From = receipt.From.ToBase58Address(network);
            this.To = receipt.To?.ToBase58Address(network);
            this.NewContractAddress = receipt.NewContractAddress?.ToBase58Address(network);
            this.ReturnValue = receipt.Result;
            this.Success = receipt.Success;
            this.Bloom = receipt.Bloom.ToString();
            this.Error = receipt.ErrorMessage;
            this.Logs = receipt.Logs.Select(x => new LogResponse(x, network)).ToArray();
        }
    }

    public class LogResponse
    {
        public string Address { get; }
        public string[] Topics { get; }
        public string Data { get; }

        public LogResponse(Log log, Network network)
        {
            this.Address = log.Address.ToBase58Address(network);
            this.Topics = log.Topics.Select(x => x.ToHexString()).ToArray();
            this.Data = log.Data.ToHexString();
        }
    }
}
