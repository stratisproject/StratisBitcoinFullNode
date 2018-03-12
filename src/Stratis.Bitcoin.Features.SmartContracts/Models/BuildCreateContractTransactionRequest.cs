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

        [Required(ErrorMessage = "An amount is required.")]
        public string Amount { get; set; }

        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Contract code is required.")]
        public string ContractCode { get; set; }

        [Required(ErrorMessage = "Air price is required.")]
        public string AirPrice { get; set; }

        [Required(ErrorMessage = "Air limit is required.")]
        public string AirLimit { get; set; }

        public string[] Parameters { get; set; }
    }
}
