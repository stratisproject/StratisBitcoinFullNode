using System.ComponentModel.DataAnnotations;
using System.Text;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.Wallet.Validations;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class BuildCreateContractTransactionRequest
    {
        public BuildCreateContractTransactionRequest()
        {
            this.AccountName = "account 0";
        }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        public string AccountName { get; set; }

        [Required(ErrorMessage = "An amount is required.")]
        public string Amount { get; set; }

        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Contract code is required.")]
        public string ContractCode { get; set; }

        [Range(SmartContractMempoolValidator.MinGasPrice, SmartContractFormatLogic.GasPriceMaximum)]
        public ulong GasPrice { get; set; }

        [Range(SmartContractFormatLogic.GasLimitCreateMinimum, SmartContractFormatLogic.GasLimitMaximum)]
        public ulong GasLimit { get; set; }

        [Required(ErrorMessage = "Sender is required.")]
        [IsBitcoinAddress]
        public string Sender { get; set; }

        public string[] Parameters { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(this.WalletName), this.WalletName, nameof(this.AccountName), this.AccountName));
            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(this.Password), this.Password, nameof(this.FeeAmount), this.FeeAmount));
            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(this.GasPrice), this.GasPrice, nameof(this.GasLimit), this.GasLimit));
            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(this.Sender), this.Sender, nameof(this.Parameters), this.Parameters));

            return builder.ToString();
        }
    }
}