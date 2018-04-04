using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class EncryptionProvider_Shall
    {
        [Fact]
        public void round_trip_encrypt_decrypt()
        {
            string plainText = "Hello";
            string cypherText = EncryptionProvider.EncryptString(plainText, "a password");
            string retrievedPlainText = EncryptionProvider.DecryptString(cypherText, "a password");
            retrievedPlainText.Should().Be(plainText);
        }
    }
}
