using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public abstract class BuildContractTransactionResponse
    {
        [JsonProperty(PropertyName = "fee")]
        public Money Fee { get; set; }

        [JsonProperty(PropertyName = "hex")]
        public string Hex { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        [JsonProperty(PropertyName = "success")]
        public bool Success { get; set; }

        [JsonProperty(PropertyName = "transactionId")]
        public uint256 TransactionId { get; set; }
    }

    public sealed class BuildCallContractTransactionResponse : BuildContractTransactionResponse
    {
        private BuildCallContractTransactionResponse() { }

        public static BuildCallContractTransactionResponse Failed(string message)
        {
            return new BuildCallContractTransactionResponse() { Message = message, Success = false };
        }

        public static BuildCallContractTransactionResponse Succeeded(string methodName, Transaction transaction, Money transactionFee)
        {
            return new BuildCallContractTransactionResponse()
            {
                Message = string.Format("Your CALL method {0} transaction was sent. Check the receipt using the transaction ID once it has been included in a new block.", methodName),
                Success = true,

                Hex = transaction.ToHex(),
                Fee = transactionFee,
                TransactionId = transaction.GetHash(),
            };
        }
    }

    public sealed class BuildCreateContractTransactionResponse : BuildContractTransactionResponse
    {
        [JsonProperty(PropertyName = "newContractAddress")]
        public string NewContractAddress { get; set; }

        private BuildCreateContractTransactionResponse() { }

        public static BuildCreateContractTransactionResponse Failed(string message)
        {
            return new BuildCreateContractTransactionResponse() { Message = message };
        }

        public static BuildCreateContractTransactionResponse Succeeded(Transaction transaction, Money transactionFee, string address)
        {
            return new BuildCreateContractTransactionResponse()
            {
                Message = "Your CREATE transaction was sent. Check the receipt using the transaction ID once it has been included in a new block..",
                Success = true,
                Hex = transaction.ToHex(),
                Fee = transactionFee,
                NewContractAddress = address,
                TransactionId = transaction.GetHash(),
            };
        }
    }
}