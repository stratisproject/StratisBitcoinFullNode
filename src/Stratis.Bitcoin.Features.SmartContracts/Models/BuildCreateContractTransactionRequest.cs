using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class BuildCreateContractTransactionRequest
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        [Required(ErrorMessage = "The name of the account is missing.")]
        public string AccountName { get; set; }

        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Contract code is required.")]
        public string ContractCode { get; set; }

        [Required(ErrorMessage = "Gas price is required.")]
        public string GasPrice { get; set; }

        [Required(ErrorMessage = "Gas limit is required.")]
        public string GasLimit { get; set; }

        public string Sender { get; set; }

        public string[] Parameters { get; set; }
    }
}
