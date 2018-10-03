using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class PoAHeaderSignatureTests
    {
        private readonly KeyTool tool;

        private readonly PoABlockHeaderValidator validator;

        public PoAHeaderSignatureTests()
        {
            string testRootPath = Path.Combine(Path.GetTempPath(), this.GetType().Name);
            var dataFolder = new DataFolder(testRootPath);
            this.tool = new KeyTool(dataFolder);

            this.validator = new PoABlockHeaderValidator(new ExtendedLoggerFactory());
        }

        [Fact]
        public void SignatureDoesntAffectHeaderHash()
        {
            Key key = this.tool.GeneratePrivateKey();
            PoABlockHeader header = this.CreateHeader();

            uint256 hashBefore = header.GetHash();

            this.validator.Sign(key, header);

            uint256 hashAfter = header.GetHash();

            Assert.Equal(hashBefore, hashAfter);
        }

        [Fact]
        public void VerifyHeaderSignature_SignatureIsValid()
        {
            Key key = this.tool.GeneratePrivateKey();
            PoABlockHeader header = this.CreateHeader();

            this.validator.Sign(key, header);

            bool validSig = this.validator.VerifySignature(key.PubKey, header);

            Assert.True(validSig);
        }

        [Fact]
        public void VerifyHeaderSignature_SignatureIsInvalid()
        {
            PoABlockHeader header = this.CreateHeader();
            this.validator.Sign(this.tool.GeneratePrivateKey(), header);

            bool validSig = this.validator.VerifySignature(this.tool.GeneratePrivateKey().PubKey, header);

            Assert.False(validSig);
        }

        private PoABlockHeader CreateHeader()
        {
            var header = new PoABlockHeader();
            header.HashMerkleRoot = new uint256(RandomUtils.GetBytes(32));

            return header;
        }
    }
}
