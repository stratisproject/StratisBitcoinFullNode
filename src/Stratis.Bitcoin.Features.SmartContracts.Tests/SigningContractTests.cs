using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SigningContractTests
    {
        private readonly SignedContractsPoARegTest network;

        public SigningContractTests()
        {
            this.network = new SignedContractsPoARegTest();
        }

        [Fact]
        public void SignContract()
        {
            byte[] contractCode = new byte[12];
            ECDSASignature signature = this.network.SigningContractPrivKey.SignMessageBytes(contractCode);

            Assert.True(this.network.SigningContractPubKey.VerifyMessage(contractCode, signature));
        }
    }
}
