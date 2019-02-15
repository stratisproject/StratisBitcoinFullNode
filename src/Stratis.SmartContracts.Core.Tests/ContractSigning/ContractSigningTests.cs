using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.ContractSigning;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.ContractSigning
{
    public class ContractSigningTests
    {
        private readonly IContractSigner contractSigner;
        private readonly Key privKey;
        private readonly PubKey pubKey;

        public ContractSigningTests()
        {
            this.contractSigner = new ContractSigner();
            this.privKey = new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom").DeriveExtKey().PrivateKey;
            this.pubKey = this.privKey.PubKey;
        }

        [Fact]
        public void Signature_Verifies()
        {
            byte[] fakeContractCode = new byte[2048];
            new Random().NextBytes(fakeContractCode);

            // Correctly generated signature validates correctly
            byte[] signature = this.contractSigner.Sign(this.privKey, fakeContractCode);
            Assert.True(this.contractSigner.Verify(this.pubKey, fakeContractCode, signature));

            // Code signed with wrong key fails
            var wrongKey = new Key();
            byte[] incorrectSignature = this.contractSigner.Sign(wrongKey, fakeContractCode);
            Assert.False(this.contractSigner.Verify(this.pubKey, fakeContractCode, incorrectSignature));
        }

        [Fact]
        public void Invalid_SignatureFormat_Fails()
        {
            byte[] fakeContractCode = new byte[2048];
            new Random().NextBytes(fakeContractCode);

            // Invalid signatures. Expected to be 71-73 bytes long.
            byte[] tooLongSignature = new byte[120];
            byte[] tooShortSignature = new byte[20];

            // Verification returns false with no exception.
            Assert.False(this.contractSigner.Verify(this.pubKey, fakeContractCode, tooLongSignature));
            Assert.False(this.contractSigner.Verify(this.pubKey, fakeContractCode, tooShortSignature));

            // TODO: Check exception was thrown inside. Logger + mock?
        }
    }
}
