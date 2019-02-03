using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Models
{
    public class RequestValidationTests
    {
        [Fact]
        public void CreateRequest_RangeValidation()
        {
            var request = new BuildCreateContractTransactionRequest
            {
                AccountName = "account 0",
                Amount = "2",
                ContractCode = "012345",
                FeeAmount = "0.02",
                GasLimit = SmartContractFormatRule.GasLimitCreateMinimum - 1, // Too low
                GasPrice = SmartContractFormatRule.GasPriceMaximum + 1, // Too high
                Parameters = new string[0],
                Password = "password",
                WalletName = "Jordan",
                Sender = "moRRQqumoxYxFysoNuoJg5E1S99WvrNZRX"
            };

            IList<ValidationResult> results = Validate(request);
            Assert.Equal(2, results.Count);
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCreateContractTransactionRequest.GasLimit));
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCreateContractTransactionRequest.GasPrice));

            request.GasLimit = SmartContractFormatRule.GasLimitMaximum + 1;
            request.GasPrice = SmartContractMempoolValidator.MinGasPrice - 1;

            results = Validate(request);
            Assert.Equal(2, results.Count);
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCreateContractTransactionRequest.GasLimit));
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCreateContractTransactionRequest.GasPrice));

            request.GasLimit = SmartContractFormatRule.GasLimitMaximum;
            request.GasPrice = SmartContractMempoolValidator.MinGasPrice;
            results = Validate(request);
            Assert.Empty(results);
        }

        [Fact]
        public void CallRequest_RangeValidation()
        {
            var request = new BuildCallContractTransactionRequest
            {
                AccountName = "account 0",
                Amount = "2",
                ContractAddress = "moRRQqumoxYxFysoNuoJg5E1S99WvrNZRX",
                MethodName = "MethodName",
                FeeAmount = "0.02",
                GasLimit = SmartContractFormatRule.GasLimitCallMinimum - 1, // Too low
                GasPrice = SmartContractFormatRule.GasPriceMaximum + 1, // Too high
                Parameters = new string[0],
                Password = "password",
                WalletName = "Jordan",
                Sender = "moRRQqumoxYxFysoNuoJg5E1S99WvrNZRX"
            };

            IList<ValidationResult> results = Validate(request);
            Assert.Equal(2, results.Count);
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCallContractTransactionRequest.GasLimit));
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCallContractTransactionRequest.GasPrice));

            request.GasLimit = SmartContractFormatRule.GasLimitMaximum + 1;
            request.GasPrice = SmartContractMempoolValidator.MinGasPrice - 1;

            results = Validate(request);
            Assert.Equal(2, results.Count);
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCallContractTransactionRequest.GasLimit));
            Assert.Contains(results, x => x.MemberNames.First() == nameof(BuildCallContractTransactionRequest.GasPrice));

            request.GasLimit = SmartContractFormatRule.GasLimitMaximum;
            request.GasPrice = SmartContractMempoolValidator.MinGasPrice;
            results = Validate(request);
            Assert.Empty(results);
        }

        [Fact]
        public void CreateRequest_AddressValidation()
        {
            var request = new BuildCreateContractTransactionRequest
            {
                AccountName = "account 0",
                Amount = "2",
                ContractCode = "012345",
                FeeAmount = "0.02",
                GasLimit = SmartContractFormatRule.GasLimitCreateMinimum,
                GasPrice = SmartContractFormatRule.GasPriceMaximum,
                Parameters = new string[0],
                Password = "password",
                WalletName = "Jordan",
                Sender = "Test"
            };

            IList<ValidationResult> results = Validate(request);
            Assert.Equal(1, results.Count);
            Assert.Equal("Invalid address", results[0].ErrorMessage); // Note that the IsBitcoinAddress doesn't currently bind to the member.
        }

        [Fact]
        public void CallRequest_AddressValidation()
        {
            var request = new BuildCallContractTransactionRequest
            {
                AccountName = "account 0",
                Amount = "2",
                ContractAddress = "Test",
                MethodName = "Test",
                FeeAmount = "0.02",
                GasLimit = SmartContractFormatRule.GasLimitCallMinimum,
                GasPrice = SmartContractFormatRule.GasPriceMaximum,
                Parameters = new string[0],
                Password = "password",
                WalletName = "Jordan",
                Sender = "Test"
            };

            IList<ValidationResult> results = Validate(request);
            Assert.Equal(2, results.Count);
            Assert.Equal("Invalid address", results[0].ErrorMessage); // Note that the IsBitcoinAddress doesn't currently bind to the member.
            Assert.Equal("Invalid address", results[1].ErrorMessage);
        }

        private static IList<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(x => x.GetService(typeof(Network))).Returns(new SmartContractsRegTest());
            var validationContext = new ValidationContext(model, serviceProvider.Object, null);
            Validator.TryValidateObject(model, validationContext, results, true);
            return results;
        }
    }
}
