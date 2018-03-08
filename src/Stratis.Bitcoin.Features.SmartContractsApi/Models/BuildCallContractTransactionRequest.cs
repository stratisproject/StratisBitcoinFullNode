using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.SmartContractsApi.Models
{
    public class BuildCallContractTransactionRequest
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        [Required(ErrorMessage = "The name of the account is missing.")]
        public string AccountName { get; set; }

        [Required(ErrorMessage = "A destination address is required.")]
        public string ContractAddress { get; set; }

        [Required(ErrorMessage = "A method name is required.")]
        public string MethodName { get; set; }

        [Required(ErrorMessage = "An amount is required.")]
        public string Amount { get; set; }

        [MoneyFormat(isRequired: true, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Air price is required.")]
        public string AirPrice { get; set; }

        [Required(ErrorMessage = "Air limit is required.")]
        public string AirLimit { get; set; }
    }
}
