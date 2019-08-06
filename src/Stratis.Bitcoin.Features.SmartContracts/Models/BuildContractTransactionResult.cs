using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class BuildContractTransactionResult
    {
        private BuildContractTransactionResult(WalletBuildTransactionModel model)
        {
            this.Response = model;
        }

        private BuildContractTransactionResult(string error, string message)
        {
            this.Error = error;
            this.Message = message;
        }

        public WalletBuildTransactionModel Response { get; }

        public string Error { get; }

        public string Message { get; }

        public static BuildContractTransactionResult Success(WalletBuildTransactionModel model)
        {
            return new BuildContractTransactionResult(model);
        }

        public static BuildContractTransactionResult Failure(string error, string message)
        {
            return new BuildContractTransactionResult(error, message);
        }
    }
}