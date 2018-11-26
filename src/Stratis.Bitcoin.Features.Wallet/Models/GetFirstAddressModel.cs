using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class GetFirstAddressModel : RequestModel
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
}
