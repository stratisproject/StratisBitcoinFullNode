using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using NBitcoin;
using FluentAssertions;
using Stratis.Bitcoin.Features.Wallet.Models;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class BuildTransactionRequestTests
    {
        [Fact]
        public void OpReturn_Longer_Than_32_Characters_Should_Fail()
        {
            var model = new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Amount = new Money(150000).ToString(),
                DestinationAddress = new Key().PubKey.GetAddress(Network.Main).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet",
                OpReturnData = "this text is exactly of length 33"
            };

            var context = new ValidationContext(model, null, null);
            var results = new List<ValidationResult>();

            var valid = Validator.TryValidateObject(model, context, results, true);

            valid.Should().BeFalse();
            results.Count.Should().Be(1);
            var theResult = results.Single();
            theResult.ErrorMessage.Should().Be("OpReturnData cannot exceed 32 characters.");
            theResult.MemberNames.Single().Should().Be("OpReturnData");
        }

        [Fact]
        public void OpReturn_Shorter_Than_32_Characters_Should_Work()
        {
            var model = new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Amount = new Money(150000).ToString(),
                DestinationAddress = new Key().PubKey.GetAddress(Network.Main).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet",
                OpReturnData = "this string is exactly 32 chars"
            };

            var context = new ValidationContext(model, null, null);
            var results = new List<ValidationResult>();

            var valid = Validator.TryValidateObject(model, context, results, true);

            valid.Should().BeTrue();
            results.Count.Should().Be(0);
        }
    }


}
