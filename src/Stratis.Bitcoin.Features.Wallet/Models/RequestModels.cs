using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Validations;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

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

        /// <summary>
        /// This password is used to encrypt the wallet for secure storage. The password is required.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <summary>
        /// This passphrase is used as an additional seed (word) joined together with the <see cref="Mnemonic"/>.
        /// </summary>
        /// <remarks>
        /// Empty string is a valid passphrase.
        /// </remarks>
        [Required(ErrorMessage = "A passphrase is required.", AllowEmptyStrings = true)]
        public string Passphrase { get; set; }

        [Required(ErrorMessage = "The name of the wallet to create is missing.")]
        public string Name { get; set; }
    }

    public class WalletLoadRequest : RequestModel
    {
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }
    }

    public class WalletRecoveryRequest : RequestModel
    {
        [Required(ErrorMessage = "A mnemonic is required.")]
        public string Mnemonic { get; set; }

        /// <summary>
        /// Supply the password that was used to create the wallet.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <summary>
        /// Supply the passphrase that was used when account was created.
        /// </summary>
        /// <remarks>
        /// If the wallet was created before <see cref="Passphrase"/> was available, set the passphrase to be the same as the password.
        /// </remarks>
        [Required(ErrorMessage = "A passphrase is required.", AllowEmptyStrings = true)]
        public string Passphrase { get; set; }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }

        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime CreationDate { get; set; }
    }

    public class WalletExtPubRecoveryRequest : RequestModel
    {
        [Required(ErrorMessage = "An extended public key is required.")]
        public string ExtPubKey { get; set; }

        [Required(ErrorMessage = "An account number is required. E.g. 0.")]
        public int AccountIndex { get; set; }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }

        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime CreationDate { get; set; }
    }

    public class WalletHistoryRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        public string AccountName { get; set; }
    }

    public class WalletBalanceRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        public string AccountName { get; set; }
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

    /// <summary>
    /// Model object to use as input to the Api request for getting the balance for an address.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Wallet.Models.RequestModel" />
    public class ReceivedByAddressRequest : RequestModel
    {
        [Required(ErrorMessage = "An address is required.")]
        public string Address { get; set; }
    }

    public class WalletName : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }
    }

    /// <summary>
    /// Model object for <see cref="WalletController.GetTransactionFeeEstimate"/> request.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Wallet.Models.RequestModel" />
    public class TxFeeEstimateRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        [Required(ErrorMessage = "The name of the account is missing.")]
        public string AccountName { get; set; }

        [Required(ErrorMessage = "A destination address is required.")]
        [IsBitcoinAddress()]
        public string DestinationAddress { get; set; }

        [Required(ErrorMessage = "An amount is required.")]
        [MoneyFormat(ErrorMessage = "The amount is not in the correct format.")]
        public string Amount { get; set; }

        public string FeeType { get; set; }

        public bool AllowUnconfirmed { get; set; }

        public bool? ShuffleOutputs { get; set; }
    }

    public class BuildTransactionRequest : TxFeeEstimateRequest
    {
        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        public string OpReturnData { get; set; }
    }

    public class SendTransactionRequest : RequestModel
    {
        public SendTransactionRequest()
        {
        }

        public SendTransactionRequest(string transactionHex)
        {
            this.Hex = transactionHex;
        }

        [Required(ErrorMessage = "A transaction in hexadecimal format is required.")]
        public string Hex { get; set; }
    }

    /// <summary>
    /// Model object to use as input to the Api request for removing transactions from a wallet.
    /// </summary>
    /// <seealso cref="RequestModel" />
    public class RemoveTransactionsModel : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is required.")]
        public string WalletName { get; set; }

        [FromQuery(Name = "ids")]
        public IEnumerable<string> TransactionsIds { get; set; }

        [FromQuery(Name = "all")]
        public bool DeleteAll { get; set; }

        [JsonProperty(PropertyName = "reSync")]
        public bool ReSync { get; set; }
    }
    public class ListAccountsModel : RequestModel
    {
        /// <summary>
        /// The name of the wallet for which to list the accounts.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is required.")]
        public string WalletName { get; set; }
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

    public class GetUnusedAddressesModel : RequestModel
    {
        [Required]
        public string WalletName { get; set; }

        [Required]
        public string AccountName { get; set; }

        [Required]
        public string Count { get; set; }
    }

    public class GetAllAddressesModel : RequestModel
    {
        [Required]
        public string WalletName { get; set; }

        [Required]
        public string AccountName { get; set; }
    }

    public class GetExtPubKeyModel : RequestModel
    {
        [Required]
        public string WalletName { get; set; }

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

    /// <summary>
    /// Object used to synchronize a wallet
    /// </summary>
    public class WalletSyncFromDateRequest : RequestModel
    {
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Date { get; set; }
    }
}
