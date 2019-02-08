using System.ComponentModel.DataAnnotations;
using System.Text;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.Wallet.Validations;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    /// <summary>
    /// A class containing the necessary parameters to perform a smart contract methods call request.
    /// </summary>
    public class LocalCallContractRequest 
    {
        /// <summary>
        /// The address of the smart contract containing the method.
        /// </summary>
        [Required(ErrorMessage = "A destination address is required.")]
        [IsBitcoinAddress]
        public string ContractAddress { get; set; }

        /// <summary>
        /// The name of the method to call.
        /// </summary>
        [Required(ErrorMessage = "A method name is required.")]
        public string MethodName { get; set; }

        /// <summary>
        /// The amount of STRAT to send to the smart contract address.
        /// </summary>
        public string Amount { get; set; }

        /// <summary>
        /// The gas price in Satoshi to use. This is used to calculate the expected expenditure 
        /// if the method is run by a miner mining a call transaction rather than
        /// locally.  
        /// </summary>
        [Range(SmartContractFormatRule.GasPriceMinimum, SmartContractFormatRule.GasPriceMaximum)]
        public ulong GasPrice { get; set; }

        /// <summary>
        /// The limit of the gas charge in Satoshi. This limit cannnot be exceeded when the method is 
        /// run locally although the gas expenditure is theorectical rather than actual.
        /// </summary>
        [Range(SmartContractFormatRule.GasLimitCallMinimum, SmartContractFormatRule.GasLimitMaximum)]
        public ulong GasLimit { get; set; }


        /// <summary>
        /// A STRAT address containing the funds to cover transaction fees, gas, and any funds specified in the
        /// Amoount field.
        /// </summary>
        [IsBitcoinAddress]
        public string Sender { get; set; }

        /// <summary>
        /// An array of strings containing the parameters to pass to the method when it is called. More information the
        /// format of a parameters string is available
        /// <a target="_blank" href="https://academy.stratisplatform.com/SmartContracts/working-with-contracts.html#parameter-serialization">here</a>.
        /// </summary> 
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