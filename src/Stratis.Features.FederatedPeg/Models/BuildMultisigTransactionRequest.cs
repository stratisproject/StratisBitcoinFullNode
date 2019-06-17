using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Features.FederatedPeg.Models
{
    /// <summary>
    /// A class containing the necessary parameters for a build transaction request.
    /// </summary>
    public class BuildMultisigTransactionRequest
    {
        /// <summary>
        /// A list of transaction recipient addresses and amounts.
        /// </summary> 
        [Required(ErrorMessage = "A list of recipients is required.")]
        [MinLength(1)]
        public List<RecipientModel> Recipients { get; set; }

        [Required(ErrorMessage = "Mnemonic phrases are required.")]
        public SecretModel[] Secrets { get; set; }
    }

    public class SecretModel
    {
        [Required(ErrorMessage = "Mnemonic is required.")]
        public string Mnemonic { get; set; }

        public string Passphrase { get; set; }
    }
}
