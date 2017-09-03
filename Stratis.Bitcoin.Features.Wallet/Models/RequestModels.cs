using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Features.Wallet.Validations;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class HashModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Hash { get; set; }
    }

    public class RequestModel
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// Object used to create a new wallet
    /// </summary>
    public class WalletCreationRequest : RequestModel
    {
        public string Mnemonic { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        public string Network { get; set; }

        public string FolderPath { get; set; }

        [Required(ErrorMessage = "The name of the wallet to create is missing.")]
        public string Name { get; set; }
    }

    public class WalletLoadRequest : RequestModel
    {
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        public string FolderPath { get; set; }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }
    }

    public class WalletRecoveryRequest : RequestModel
    {
        [Required(ErrorMessage = "A mnemonic is required.")]
        public string Mnemonic { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        public string FolderPath { get; set; }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }

        public string Network { get; set; }

        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime CreationDate { get; set; }
    }

    public class WalletHistoryRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }
    }

    public class WalletBalanceRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }
    }

    /// <summary>
    /// Model object to use as input to the Api request for getting the maximum spendable amount on an account. 
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Wallet.Models.RequestModel" />
    public class WalletMaximumBalanceRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        [Required(ErrorMessage = "The name of the account is missing.")]
        public string AccountName { get; set; }

        [Required(ErrorMessage = "A fee type is required. It can be 'low', 'medium' or 'high'.")]
        public string FeeType { get; set; }

        public bool AllowUnconfirmed { get; set; }
    }

    public class WalletName : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }
    }

    public class BuildTransactionRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }
        
        [Required(ErrorMessage = "The name of the account is missing.")]
        public string AccountName { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "A destination address is required.")]
        [IsBitcoinAddress()]
        public string DestinationAddress { get; set; }

        [Required(ErrorMessage = "An amount is required.")]
        public string Amount { get; set; }

        [Required(ErrorMessage = "A fee type is required. It can be 'low', 'medium' or 'high'.")]
        public string FeeType { get; set; }

        public bool AllowUnconfirmed { get; set; }
    }

    public class SendTransactionRequest : RequestModel
    {
        [Required(ErrorMessage = "A transaction in hexadecimal format is required.")]
        public string Hex { get; set; }
    }

    public class GetUnusedAddressModel : RequestModel
    {
        /// <summary>
        /// The name of the wallet from which to get the address.
        /// </summary>
        [Required]
        public string WalletName { get; set; }
        
        /// <summary>
        /// The name of the account for which to get the address.
        /// </summary>
        [Required]
        public string AccountName { get; set; }
    }

    public class GetUnusedAccountModel : RequestModel
    {
        /// <summary>
        /// The name of the wallet in which to create the account.
        /// </summary>
        [Required]
        public string WalletName { get; set; }
        
        /// <summary>
        /// The password for this wallet.
        /// </summary>
        [Required]
        public string Password { get; set; }
    }
}
