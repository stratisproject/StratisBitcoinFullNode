using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace NBitcoin.Tests
{
    public class ProvenBlockHeaderTests
    {
        private readonly PosConsensusFactory factory = new PosConsensusFactory();

        [Fact]
        public void ProvenBlockHeaderShouldSerializeAndDeserializeCorrectly()
        {
            // Setup new header to serialize with some fake properties
            ProvenBlockHeader provenHeaderToSerialize = this.CreateNewProvenBlockHeaderMock();
            provenHeaderToSerialize.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1));
            provenHeaderToSerialize.Bits = 1;
            provenHeaderToSerialize.Nonce = 2;

            // Attempt to serialize it
            using (var ms = new MemoryStream())
            {
                provenHeaderToSerialize.ReadWrite(new BitcoinStream(ms, true));

                byte[] bytes = ms.ToArray();
                bytes.Should().HaveCountGreaterThan(0);

                // Setup another slightly different header and try to load it from
                // serialized data from original header
                ProvenBlockHeader provenHeaderToDeserialize = this.CreateNewProvenBlockHeaderMock();
                provenHeaderToDeserialize.GetHash().Should().NotBe(provenHeaderToSerialize.GetHash());

                // Attempt to deserialize it
                provenHeaderToDeserialize.ReadWrite(bytes, this.factory);
                provenHeaderToDeserialize.GetHash().Should().Be(provenHeaderToSerialize.GetHash());
            }
        }

        [Fact]
        public void ShouldNotBeAbleToCreateProvenBlockHeaderFromANullBlock()
        {
            Action createProvenHeader = () => this.factory.CreateProvenBlockHeader(null);
            createProvenHeader.Should().Throw<ArgumentNullException>();
        }

        private ProvenBlockHeader CreateNewProvenBlockHeaderMock()
        {
            var block = new PosBlock(this.factory.CreateBlockHeader());
            block.Transactions.Add(new Transaction()); // coinbase
            block.Transactions.Add(new Transaction()); // coinstake

            ProvenBlockHeader provenBlockHeader = this.factory.CreateProvenBlockHeader(block);

            return provenBlockHeader;
        }
    }
}
