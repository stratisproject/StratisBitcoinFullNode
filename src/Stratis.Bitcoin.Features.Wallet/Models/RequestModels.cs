using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Features.Wallet.Validations;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// A class containing the necessary parameters for a wallet resynchronization request
    /// which takes the hash of the block to resync after. 
    /// </summary>
    public class HashModel
    {
        /// <summary>
        /// The hash of the block to resync after. 
        /// </summary>
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
    /// A class containing the necessary parameters for a create wallet request.
    /// </summary>
    public class WalletCreationRequest : RequestModel
    {
        /// <summary>
        /// The mnemonic used to create the HD wallet.
        /// </summary>
        public string Mnemonic { get; set; }

        /// <summary>
        /// A password used to encrypt the wallet for secure storage.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <summary>
        /// An optional additional seed, which is joined together with the <see cref="Mnemonic"/>
        /// when the wallet is created.
        /// Although you will be prompted to enter a passphrase, an empty string is still valid.
        /// </summary>
        /// <remarks>
        /// The passphrase can be an empty string.
        /// </remarks>
        [Required(ErrorMessage = "A passphrase is required.", AllowEmptyStrings = true)]
        public string Passphrase { get; set; }

        /// <summary>
        /// The name of the wallet.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet to create is missing.")]
        public string Name { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a load wallet request.
    /// </summary>
    public class WalletLoadRequest : RequestModel
    {
        /// <summary>
        /// The password that was used to create the wallet.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <summary>
        /// The name of the wallet.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a wallet recovery request.
    /// </summary>
    public class WalletRecoveryRequest : RequestModel
    {

        /// <summary>
        /// The mnemonic that was used to create the wallet.
        /// </summary>
        [Required(ErrorMessage = "A mnemonic is required.")]
        public string Mnemonic { get; set; }

        /// <summary>
        /// The password that was used to create the wallet.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <summary>
        /// The passphrase that was used to create the wallet.
        /// </summary>
        /// <remarks>
        /// If the wallet was created before <see cref="Passphrase"/> was available as a parameter, set the passphrase to be the same as the password.
        /// </remarks>
        [Required(ErrorMessage = "A passphrase is required.", AllowEmptyStrings = true)]
        public string Passphrase { get; set; }

        /// <summary>
        /// The name of the wallet.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }

        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime CreationDate { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a wallet recovery request using its extended public key.
    /// Note that the recovered wallet will not have a private key and is
    /// only suitable for returning the wallet history using further API calls. As such,
    /// only the extended public key is used in the recovery process.
    /// </summary>
    public class WalletExtPubRecoveryRequest : RequestModel
    {
        /// <summary>
        /// The extended public key used by the wallet.
        /// </summary>
        [Required(ErrorMessage = "An extended public key is required.")]
        public string ExtPubKey { get; set; }

        /// <summary>
        /// The index of the account to generate for the wallet. For example, specifying a value of 0
        /// generates "account0".
        /// </summary>
        [Required(ErrorMessage = "An account number is required. E.g. 0.")]
        public int AccountIndex { get; set; }

        /// <summary>
        /// The name to give the recovered wallet.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }

        /// <summary>
        /// The creation date and time to give the recovered wallet. 
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime CreationDate { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a wallet history request.
    /// </summary>
    public class WalletHistoryRequest : RequestModel
    {
        public WalletHistoryRequest()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet to recover the history for.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        /// <summary>
        /// Optional. The name of the account to recover the history for. If no account name is specified,
        /// the entire history of the wallet is recovered.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// Optional. If set, will filter the transaction history for all transactions made to or from the given address.
        /// </summary>
        [IsBitcoinAddress(Required = false)]
        public string Address { get; set; }

        /// <summary>
        /// An optional value allowing (with Take) pagination of the wallet's history. If given,
        /// the member specifies the numbers of records in the wallet's history to skip before
        /// beginning record retrieval; otherwise the wallet history records are retrieved starting from 0.
        /// </summary>      
        public int? Skip { get; set; }

        /// <summary>
        /// An optional value allowing (with Skip) pagination of the wallet's history. If given,
        /// the member specifies the number of records in the wallet's history to retrieve in this call; otherwise all
        /// wallet history records are retrieved.
        /// </summary>  
        public int? Take { get; set; }

        /// <summary>
        /// An optional string that can be used to match different data in the transaction records.
        /// It is possible to match on the following: the transaction ID, the address at which funds where received,
        /// and the address to which funds where sent.
        /// </summary>
        [JsonProperty(PropertyName = "q")]
        public string SearchQuery { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a wallet balance request.
    /// </summary>
    public class WalletBalanceRequest : RequestModel
    {
        public WalletBalanceRequest()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet to retrieve the balance for.
        /// </summary> 
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account to retrieve the balance for. If no account name is supplied,
        /// then the balance for the entire wallet (all accounts) is retrieved.
        /// </summary>         
        public string AccountName { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a request to get the maximum
    /// spendable amount for a specific wallet account.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Wallet.Models.RequestModel" />
    public class WalletMaximumBalanceRequest : RequestModel
    {
        public WalletMaximumBalanceRequest()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet to retrieve the maximum spendable amount for.
        /// </summary> 
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account to retrieve the maximum spendable amount for.
        /// </summary>   
        public string AccountName { get; set; }

        /// <summary>
        /// The type of fee to use when working out the fee required to spend the amount.
        /// Specify "low", "medium", or "high".
        /// </summary>        
        [Required(ErrorMessage = "A fee type is required. It can be 'low', 'medium' or 'high'.")]
        public string FeeType { get; set; }

        /// <summary>
        /// A flag that specifies whether to include the unconfirmed amounts held at account addresses
        /// as spendable.
        /// </summary> 
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
    /// A class containing the necessary parameters for a transaction fee estimate request.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Wallet.Models.RequestModel" />
    public class TxFeeEstimateRequest : RequestModel
    {
        public TxFeeEstimateRequest()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet containing the UTXOs to use in the transaction.
        /// </summary> 
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account containing the UTXOs to use in the transaction.
        /// </summary> 
        public string AccountName { get; set; }

        /// <summary>
        /// A list of outpoints to use as inputs for the transaction.
        /// </summary> 
        public List<OutpointRequest> Outpoints { get; set; }

        /// <summary>
        /// A list of transaction recipients. For each recipient, specify the Pubkey script and the amount the
        /// recipient will receive in STRAT (or a sidechain coin). If the transaction was realized,
        /// both the values would be used to create the UTXOs for the transaction recipients.
        /// </summary> 
        [Required(ErrorMessage = "A list of recipients is required.")]
        [MinLength(1)]
        public List<RecipientModel> Recipients { get; set; }

        /// <summary>
        /// A string containing any OP_RETURN output data to store as part of the transaction.
        /// </summary>       
        public string OpReturnData { get; set; }

        /// <summary>
        /// The funds in STRAT (or a sidechain coin) to include with the OP_RETURN output. Currently, specifying
        /// some funds helps OP_RETURN outputs be relayed around the network.
        /// </summary>   
        [MoneyFormat(isRequired: false, ErrorMessage = "The op return amount is not in the correct format.")]
        public string OpReturnAmount { get; set; }

        /// <summary>
        /// The type of fee to use when working out the fee for the transaction. Specify "low", "medium", or "high".
        /// </summary>  
        public string FeeType { get; set; }

        /// <summary>
        /// A flag that specifies whether to include the unconfirmed amounts as inputs to the transaction.
        /// If this flag is not set, at least one confirmation is required for each input.
        /// </summary> 
        public bool AllowUnconfirmed { get; set; }

        /// <summary>
        /// A flag that specifies whether to shuffle the transaction outputs for increased privacy. Randomizing the
        /// the order in which the outputs appear when the transaction is being built stops it being trivial to
        /// determine whether a transaction output is payment or change. This helps defeat unsophisticated
        /// chain analysis algorithms. 
        /// Defaults to true.
        /// </summary>         
        public bool? ShuffleOutputs { get; set; }

        /// <summary>
        /// The address to which the change from the transaction should be returned. If this is not set,
        /// the default behaviour from the <see cref="WalletTransactionHandler"/> will be used to determine the change address.
        /// </summary>
        [IsBitcoinAddress(Required = false)]
        public string ChangeAddress { get; set; }
    }

    public class OutpointRequest : RequestModel
    {
        /// <summary>
        /// The transaction ID.
        /// </summary>
        [Required(ErrorMessage = "The transaction id is missing.")]
        public string TransactionId { get; set; }

        /// <summary>
        /// The index of the output in the transaction.
        /// </summary>
        [Required(ErrorMessage = "The index of the output in the transaction is missing.")]
        public int Index { get; set; }
    }

    public class RecipientModel
    {
        /// <summary>
        /// The destination address.
        /// </summary>
        [Required(ErrorMessage = "A destination address is required.")]
        [IsBitcoinAddress()]
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The amount that will be sent.
        /// </summary>
        [Required(ErrorMessage = "An amount is required.")]
        [MoneyFormat(ErrorMessage = "The amount is not in the correct format.")]
        public string Amount { get; set; }
    }
 
    /// <summary>
    /// A class containing the necessary parameters for a build transaction request.
    /// </summary>
    public class BuildTransactionRequest : TxFeeEstimateRequest, IValidatableObject
    {
        /// <summary>
        /// The fee for the transaction in STRAT (or a sidechain coin).
        /// </summary>
        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }
                
        /// <summary>
        /// The password for the wallet containing the funds for the transaction.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(this.FeeAmount) && !string.IsNullOrEmpty(this.FeeType))
            {
                yield return new ValidationResult(
                    $"The query parameters '{nameof(this.FeeAmount)}' and '{nameof(this.FeeType)}' cannot be set at the same time. " +
                    $"Please use '{nameof(this.FeeAmount)}' if you'd like to set the fee manually, or '{nameof(this.FeeType)}' if you want the wallet to calculate it for you.",
                    new[] { $"{nameof(this.FeeType)}" });
            }

            if (string.IsNullOrEmpty(this.FeeAmount) && string.IsNullOrEmpty(this.FeeType))
            {
                yield return new ValidationResult(
                    $"One of parameters '{nameof(this.FeeAmount)}' and '{nameof(this.FeeType)}' is required. " +
                    $"Please use '{nameof(this.FeeAmount)}' if you'd like to set the fee manually, or '{nameof(this.FeeType)}' if you want the wallet to calculate it for you.",
                    new[] { $"{nameof(this.FeeType)}" });
            }
        }
    }

    /// <summary>
    /// A class containing the necessary parameters for a send transaction request.
    /// </summary>
    public class SendTransactionRequest : RequestModel
    {
        
        public SendTransactionRequest()
        {
        }

        
        public SendTransactionRequest(string transactionHex)
        {
            this.Hex = transactionHex;
        }

        /// <summary>
        /// A string containing the transaction in hexadecimal format.
        /// </summary>
        [Required(ErrorMessage = "A transaction in hexadecimal format is required.")]

        /// <summary>
        /// The transaction as a hexadecimal string.
        /// </summary>
        public string Hex { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a remove transactions request.
    /// </summary>
    /// <seealso cref="RequestModel" />
    public class RemoveTransactionsModel : RequestModel, IValidatableObject
    {
        /// <summary>
        /// The name of the wallet to remove the transactions from.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is required.")]
        public string WalletName { get; set; }

        /// <summary>
        /// The IDs of the transactions to remove.
        /// </summary>
        [FromQuery(Name = "ids")]
        public IEnumerable<string> TransactionsIds { get; set; }

        /// <summary>
        /// A date and time after which all transactions should be removed.
        /// </summary>        
        [JsonConverter(typeof(IsoDateTimeConverter))]
        [FromQuery(Name = "fromDate")]
        public DateTime FromDate { get; set; }

        /// <summary>
        /// A flag that specifies whether to delete all transactions from a wallet.
        /// </summary>
        [FromQuery(Name = "all")]
        public bool DeleteAll { get; set; }

        /// <summary>
        /// A flag that specifies whether to resync the wallet after removing the transactions.
        /// </summary>        
        [JsonProperty(PropertyName = "reSync")]
        public bool ReSync { get; set; }

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Check that one of the filters is set.
            if (!this.DeleteAll && (this.TransactionsIds == null || !this.TransactionsIds.Any()) && this.FromDate == default(DateTime))
            {
                yield return new ValidationResult(
                    $"One of the query parameters '{nameof(this.DeleteAll)}', '{nameof(this.TransactionsIds)}' or '{nameof(this.FromDate)}' must be set.",
                    new[] { $"{nameof(this.DeleteAll)}" });
            }

            // Check that only one of the filters is set.
            if ((this.DeleteAll && this.TransactionsIds != null) 
                || (this.DeleteAll && this.FromDate != default(DateTime))
                || (this.TransactionsIds != null && this.FromDate != default(DateTime)))
            {
                yield return new ValidationResult(
                    $"Only one out of the query parameters '{nameof(this.DeleteAll)}', '{nameof(this.TransactionsIds)}' or '{nameof(this.FromDate)}' can be set.",
                    new[] { $"{nameof(this.DeleteAll)}" });
            }

            // Check that transaction ids doesn't contain empty elements.
            if (this.TransactionsIds != null && this.TransactionsIds.Any(trx => trx == null))
            {
                yield return new ValidationResult(
                    $"'{nameof(this.TransactionsIds)}' must not contain any null ids.",
                    new[] { $"{nameof(this.TransactionsIds)}" });
            }
        }
    }

    /// <summary>
    /// A class containing the necessary parameters for a list accounts request.  
    /// </summary>
    public class ListAccountsModel : RequestModel
    {
        /// <summary>
        /// The name of the wallet for which to list the accounts.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is required.")]
        public string WalletName { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for an unused address request.  
    /// </summary>
    public class GetUnusedAddressModel : RequestModel
    {
        public GetUnusedAddressModel()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet from which to get the address.
        /// </summary>
        [Required]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account for which to get the address.
        /// </summary>
        public string AccountName { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for an unused addresses request.  
    /// </summary>
    public class GetUnusedAddressesModel : RequestModel
    {
        public GetUnusedAddressesModel()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet from which to get the addresses.
        /// </summary>
        [Required]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account for which to get the addresses.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// The number of addresses to retrieve.
        /// </summary>
        [Required]
        public string Count { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a retrieve all addresses request.  
    /// </summary>
    public class GetAllAddressesModel : RequestModel
    {
        public GetAllAddressesModel()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet from which to get the addresses.
        /// </summary>
        [Required]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account for which to get the addresses.
        /// </summary>
        public string AccountName { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for an extended public key request.  
    /// </summary>
    public class GetExtPubKeyModel : RequestModel
    {
        public GetExtPubKeyModel()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet from which to get the extended public key.
        /// </summary>
        [Required]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account for which to get the extended public key.
        /// <summary>
        public string AccountName { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a new account request.  
    /// </summary>
    public class GetUnusedAccountModel : RequestModel
    {
        /// <summary>
        /// The name of the wallet in which to create the account.
        /// </summary>
        [Required]
        public string WalletName { get; set; }

        /// <summary>
        /// The password for the wallet.
        /// </summary>
        [Required]
        public string Password { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters for a wallet resynchronization request.  
    /// </summary>
    public class WalletSyncFromDateRequest : RequestModel
    {
        /// <summary>
        /// The date and time from which to resync the wallet.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters to perform an add address book entry request.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Wallet.Models.RequestModel" />
    public class AddressBookEntryRequest : RequestModel
    {
        /// <summary>
        /// A label to attach to the address book entry.
        /// </summary>
        [Required(ErrorMessage = "A label is required.")]
        [MaxLength(200)]
        public string Label { get; set; }

        /// <summary>
        /// The address to enter in the address book.
        /// </summary>
        [Required(ErrorMessage = "An address is required.")]
        [IsBitcoinAddress()]
        public string Address { get; set; }
    }

    /// <summary>
    /// A class containing the necessary parameters to perform a spendable transactions request.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Wallet.Models.RequestModel" />
    public class SpendableTransactionsRequest : RequestModel
    {
        public SpendableTransactionsRequest()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        /// <summary>
        /// The name of the wallet to retrieve the spendable transactions for.
        /// </summary> 
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account to retrieve the spendable transaction for. If no account name is specified,
        /// the entire history of the wallet is recovered.
        public string AccountName { get; set; }

        /// <summary>
        /// The minimum number of confirmations a transaction needs to have to be included.
        /// To include unconfirmed transactions, set this value to 0.
        /// </summary>
        public int MinConfirmations { get; set; }
    }

    public class SplitCoinsRequest : RequestModel
    {
        public SplitCoinsRequest()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        public string AccountName { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string WalletPassword { get; set; }

        /// <summary>The amount that will be sent.</summary>
        [Required(ErrorMessage = "An amount is required.")]
        [MoneyFormat(ErrorMessage = "The amount is not in the correct format.")]
        public string TotalAmountToSplit { get; set; }

        [Required]
        public int UtxosCount { get; set; }
    }

    /// <summary>
    /// Object to sign a message.
    /// </summary>
    public class SignMessageRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "An address is required.")]
        public string ExternalAddress { get; set; }

        [Required(ErrorMessage = "A message is required.")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Object to verify a signed message.
    /// </summary>
    public class VerifyRequest : RequestModel
    {
        [Required(ErrorMessage = "A signature is required.")]
        public string Signature { get; set; }

        [Required(ErrorMessage = "An address is required.")]
        public string ExternalAddress { get; set; }

        [Required(ErrorMessage = "A message is required.")]
        public string Message { get; set; }
    }
}
