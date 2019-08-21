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

        /// <summary>
        /// A response that gets constructed when building the call contract transaction failed.
        /// </summary>
        /// <param name="message">The error message indicating what went wrong.</param>
        public static BuildCallContractTransactionResponse Failed(string message)
        {
            return new BuildCallContractTransactionResponse() { Message = message, Success = false };
        }

        /// <summary>
        /// Constructs a response if the call contract transaction was successfully built.
        /// </summary>
        /// <param name="methodName">The method name that will be called on the contract.</param>
        /// <param name="transaction">The created call contract transaction.</param>
        /// <param name="transactionFee">The fee associated with the transaction.</param>
        public static BuildCallContractTransactionResponse Succeeded(string methodName, Transaction transaction, Money transactionFee)
        {
            return new BuildCallContractTransactionResponse()
            {
                Success = true,
                Message = string.Format("Your CALL method {0} transaction was successfully built.", methodName),
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

        /// <summary>
        /// A response that gets constructed when building the create contract transaction failed.
        /// </summary>
        /// <param name="message">The error message indicating what went wrong.</param>
        public static BuildCreateContractTransactionResponse Failed(string message)
        {
            return new BuildCreateContractTransactionResponse() { Message = message };
        }

        /// <summary>
        /// Constructs a response if the create contract transaction was successfully built.
        /// </summary>
        /// <param name="transaction">The created create contract transaction.</param>
        /// <param name="transactionFee">The fee associated with the transaction.</param>
        /// <param name="address">The address associated to the new contract.</param>
        public static BuildCreateContractTransactionResponse Succeeded(Transaction transaction, Money transactionFee, string address)
        {
            return new BuildCreateContractTransactionResponse()
            {
                Message = "Your CREATE contract transaction was successfully built.",
                Success = true,
                Hex = transaction.ToHex(),
                Fee = transactionFee,
                NewContractAddress = address,
                TransactionId = transaction.GetHash(),
            };
        }
    }
}