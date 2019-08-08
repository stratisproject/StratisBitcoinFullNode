using System;
using NBitcoin;
using Stratis.Features.Collateral;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CollateralHeightCommitmentEncoderTests
    {
        private CollateralHeightCommitmentEncoder encoder;

        private Random r;

        public CollateralHeightCommitmentEncoderTests()
        {
            this.encoder = new CollateralHeightCommitmentEncoder();
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
