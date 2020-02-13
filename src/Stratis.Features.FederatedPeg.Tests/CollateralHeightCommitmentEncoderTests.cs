using System;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Features.Collateral;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CollateralHeightCommitmentEncoderTests
    {
        private readonly CollateralHeightCommitmentEncoder encoder;

        private readonly Random r;

        public CollateralHeightCommitmentEncoderTests()
        {
            ILogger logger = new Mock<ILogger>().Object;
            this.encoder = new CollateralHeightCommitmentEncoder(logger);
            this.r = new Random();
        }

        [Fact]
        public void CanEncodeAndDecode()
        {
            for (int i = 0; i < 1000; i++)
            {
                int randomValue = this.r.Next();

                byte[] encodedWithPrefix = this.encoder.EncodeWithPrefix(randomValue);

                var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(encodedWithPrefix));
                var tx = new Transaction();
                tx.AddOutput(Money.Zero, votingOutputScript);

                byte[] rawData = this.encoder.ExtractRawCommitmentData(tx);

                int decodedValue = this.encoder.Decode(rawData);

                Assert.Equal(randomValue, decodedValue);
            }
        }
    }
}
