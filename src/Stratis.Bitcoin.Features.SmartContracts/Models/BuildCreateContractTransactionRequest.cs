using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class BuildCreateContractTransactionRequest
    {
        public BuildCreateContractTransactionRequest()
        {
            this.AccountName = "account 0";
            this.GasPrice = "1";
            this.GasLimit = "10000";
        }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        public string AccountName { get; set; }

        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Contract code is required.")]
        public string ContractCode { get; set; }

        [Range(GasBudgetRule.GasPriceMinimum, GasBudgetRule.GasPriceMaximum)]
        public string GasPrice { get; set; }

        [Range(GasBudgetRule.GasLimitMinimum, GasBudgetRule.GasLimitMaximum)]
        public string GasLimit { get; set; }

        [Required(ErrorMessage = "Sender is required.")]
        public string Sender { get; set; }

        public string[] Parameters { get; set; }
    }
}