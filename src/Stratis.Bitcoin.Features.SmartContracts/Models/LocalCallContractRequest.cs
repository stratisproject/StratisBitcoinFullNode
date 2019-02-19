using System.ComponentModel.DataAnnotations;
using System.Text;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.Wallet.Validations;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class LocalCallContractRequest
    {
        [Required(ErrorMessage = "A destination address is required.")]
        [IsBitcoinAddress]
        public string ContractAddress { get; set; }

        [Required(ErrorMessage = "A method name is required.")]
        public string MethodName { get; set; }

        public string Amount { get; set; }

        [Range(SmartContractFormatLogic.GasPriceMinimum, SmartContractFormatLogic.GasPriceMaximum)]
        public ulong GasPrice { get; set; }

        [Range(SmartContractFormatLogic.GasLimitCallMinimum, SmartContractFormatLogic.GasLimitMaximum)]
        public ulong GasLimit { get; set; }

        [IsBitcoinAddress]
        public string Sender { get; set; }

        public string[] Parameters { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3}", nameof(this.GasPrice), this.GasPrice, nameof(this.GasLimit), this.GasLimit));
            builder.Append(string.Format("{0}:{1},{2}:{3}", nameof(this.Sender), this.Sender, nameof(this.Parameters), this.Parameters));

            return builder.ToString();
        }
    }
}