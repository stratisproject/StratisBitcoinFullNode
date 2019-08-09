using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Features.Wallet.Validations;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class ScTxFeeEstimateRequest : TxFeeEstimateRequest
    {
        [Required(ErrorMessage = "Sender is required.")]
        [IsBitcoinAddress]
        public string Sender { get; set; }
    }

    public class BuildContractTransactionRequest : BuildTransactionRequest
    {
        [Required(ErrorMessage = "Sender is required.")]
        [IsBitcoinAddress]
        public string Sender { get; set; }
    }
}