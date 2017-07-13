using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Stratis.Bitcoin.Wallet.Validations
{
    public class IsBitcoinAddressAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var network = (Network)validationContext.GetService(typeof(Network));
            try
            {
                BitcoinAddress.Create(value as string, network);
                return ValidationResult.Success;
            }
            catch { return new ValidationResult("Invalid address"); }
        }
    }
}
