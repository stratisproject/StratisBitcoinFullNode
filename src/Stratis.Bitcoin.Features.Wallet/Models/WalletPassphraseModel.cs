using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletPassphraseModel
    {
        [Required]
        public string Passphrase { get; set; }
        
        public int Seconds { get; set; }
    }
}
