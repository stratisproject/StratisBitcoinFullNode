using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class GetColdStakingAddressRequest
    {
        // The wallet name.
        [Required]
        [JsonProperty(PropertyName = "walletName")]
        public string WalletName { get; set; }

        // The wallet account.
        [Required]
        [JsonProperty(PropertyName = "walletAccount")]
        public string WalletAccount { get; set; }

        // The wallet password.
        [Required]
        [JsonProperty(PropertyName = "walletPassword")]
        public string WalletPassword { get; set; }

        // Determines from which of the cold staking accounts the address will be taken.
        [Required]
        [JsonProperty(PropertyName = "isColdWalletAddress")]
        public bool IsColdWalletAddress { get; set; }
    }

    public class GetColdStakingAddressResponse
    {
        // A Base58 cold staking address from the hot or cold wallet accounts.
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }
    }

    public class SetupColdStakingResponse
    {
        /// <summary>
        /// The transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "transactionId")]
        public uint256 TransactionId { get; set; }

        /// <summary>
        /// The list of outputs in this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "outputs")]
        public ICollection<TransactionOutputModel> Outputs { get; set; }
    }

    public class SetupColdStakingRequest
    {
        // The Base58 cold wallet address.
        [Required]
        [JsonProperty(PropertyName = "coldWalletAddress")]
        public string ColdWalletAddress { get; set; }

        // The Base58 hot wallet address.
        [Required]
        [JsonProperty(PropertyName = "hotWalletAddress")]
        public string HotWalletAddress { get; set; }

        // The wallet name.
        [Required]
        [JsonProperty(PropertyName = "walletName")]
        public string WalletName { get; set; }

        // The wallet password.
        [Required]
        [JsonProperty(PropertyName = "walletPassword")]
        public string WalletPassword { get; set; }

        // The wallet account.
        [Required]
        [JsonProperty(PropertyName = "walletAccount")]
        public string WalletAccount { get; set; }

        // The amount for cold staking.
        [Required]
        [MoneyFormat(ErrorMessage = "The amount is not in the correct format.")]
        [JsonProperty(PropertyName = "amount")]
        public string Amount { get; set; }

        // The fees for cold staking.
        [Required]
        [MoneyFormat(ErrorMessage = "The fees are not in the correct format.")]
        [JsonProperty(PropertyName = "fees")]
        public string Fees { get; set; }
    }
}
