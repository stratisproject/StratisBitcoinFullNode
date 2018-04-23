using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreControllerTests
    {
        [Fact]
        public void GetBlock_With_null_Hash_IsInvalid()
        {
            var requestWithNoHash = new BlockStore.Models.ObjectByHashQueryModel()
            {
                Hash = null,
                OutputJson = true
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeFalse();
        }

        [Fact]
        public void GetBlock_With_empty_Hash_IsInvalid()
        {
            var requestWithNoHash = new BlockStore.Models.ObjectByHashQueryModel()
            {
                Hash = "",
                OutputJson = false
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeFalse();
        }

        [Fact]
        public void GetBlock_With_good_Hash_IsValid()
        {
            var requestWithNoHash = new BlockStore.Models.ObjectByHashQueryModel()
            {
                Hash = "some good hash",
                OutputJson = true
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeTrue();
        }
    }
}
