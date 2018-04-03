using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Features.Wallet.Validations;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.SidechainWallet.Models
{
    public class BuildSidechainTransactionRequest : BuildTransactionRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "A sidechain identifier must be specified")]
        public string SidechainIdentifier { get; set; }
    }
}
