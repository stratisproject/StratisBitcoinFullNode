using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Features.Wallet.Validations;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    /// <summary>
    /// A class containing the necessary parameters to perform a smart contract creation request.
    /// </summary>
    public class BuildCreateContractTransactionRequest
    {
        public BuildCreateContractTransactionRequest()
        {
            this.AccountName = "account 0";
        }

        /// <summary>
        /// The name of the wallet containing funds to use to cover transaction fees, gas, and any funds specified in the
        /// Amount field.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the wallet account containing funds to use to cover transaction fees, gas, and any funds specified in the
        /// Amount field. Defaults to "account 0".
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// A list of outpoints to use as inputs for the transaction.
        /// </summary> 
        public List<OutpointRequest> Outpoints { get; set; }

        /// <summary>
        /// The amount of STRAT (or the sidechain coin) to send to the smart contract address on creation.
        /// </summary>
        [Required(ErrorMessage = "An amount is required.")]
        public string Amount { get; set; }

        /// <summary>
        /// The fees in STRAT (or the sidechain coin) to cover the smart contract creation transaction.
        /// </summary>
        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        /// <summary>
        /// The password for the wallet.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <summary>
        /// The smart contract bytecode.
        /// </summary>
        [Required(ErrorMessage = "Contract code is required.")]
        public string ContractCode { get; set; }

        /// <summary>
        /// The gas price to charge when the smart contract constructor is run by the miner mining the creation transaction. 
        /// </summary>
        [Range(SmartContractMempoolValidator.MinGasPrice, SmartContractFormatLogic.GasPriceMaximum)]
        public ulong GasPrice { get; set; }
         
        /// <summary>
        /// The maximum amount of gas that can be spent executing this transaction.
        /// This limit cannot be exceeded when the method is 
        /// run by the miner mining the creation transaction. If the gas spent exceeds this value, 
        /// execution of the smart contract stops.
        /// </summary>
        [Range(SmartContractFormatLogic.GasLimitCreateMinimum, SmartContractFormatLogic.GasLimitMaximum)]
        public ulong GasLimit { get; set; }

        /// <summary>
        /// A wallet address containing the funds to cover transaction fees, gas, and any funds specified in the
        /// Amount field. Some methods, such as a withdrawal method on an escrow smart contract,
        /// should only be executed by the deployer. In this case, it is this address that identifies
        /// the deployer.
        /// It is recommended that you use /api/SmartContractWallet/account-addresses to retrieve
        /// an address to use for smart contracts. This enables you to obtain a smart contract transaction history.
        /// However, any sender address containing the required funds will work.
        /// </summary>
        [Required(ErrorMessage = "Sender is required.")]
        [IsBitcoinAddress]
        public string Sender { get; set; }

        /// <summary>
        /// An array of encoded strings containing the parameters (and their type) to pass to the smart contract
        /// constructor when it is called. More information on the
        /// format of a parameter string is available
        /// <a target="_blank" href="https://academy.stratisplatform.com/SmartContracts/working-with-contracts.html#parameter-serialization">here</a>.
        /// </summary>
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