using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public sealed class VoteRequest
    {
        public VoteRequest()
        {
            this.AccountName = WalletManager.DefaultAccount;
        }

        public string AccountName { get; set; }

        [Required(ErrorMessage = "A vote is required.")]
        [MaxLength(3)]
        public string Vote { get; set; }

        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string WalletPassword { get; set; }
    }
}