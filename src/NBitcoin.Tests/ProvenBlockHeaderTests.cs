using System;
using System.IO;
using FluentAssertions;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace NBitcoin.Tests
{
    public class ProvenBlockHeaderTests
    {
        private readonly PosConsensusFactory factory = new PosConsensusFactory();
        private readonly Network network = KnownNetworks.StratisTest;

        [Fact]
        public void ProvenBlockHeaderShouldSerializeAndDeserializeCorrectly()
        {
            // Setup new header to serialize with some fake properties.
            ProvenBlockHeader provenHeaderToSerialize = this.CreateNewProvenBlockHeaderMock();
            provenHeaderToSerialize.BlockTime = new DateTimeOffset(new DateTime(2018, 1, 1));
            provenHeaderToSerialize.Bits = 1;
            provenHeaderToSerialize.Nonce = 2;

            // Attempt to serialize it.
            using (var ms = new MemoryStream())
            {
                provenHeaderToSerialize.ReadWrite(new BitcoinStream(ms, true));

                byte[] bytes = ms.ToArray();
                bytes.Should().HaveCountGreaterThan(0);

                // Setup another slightly different header and try to load it from
                // serialized data from original header.
                ProvenBlockHeader provenHeaderToDeserialize = this.CreateNewProvenBlockHeaderMock();
                provenHeaderToDeserialize.GetHash().Should().NotBe(provenHeaderToSerialize.GetHash());

                // Attempt to deserialize it.
                provenHeaderToDeserialize.ReadWrite(bytes, this.factory);
                provenHeaderToDeserialize.GetHash().Should().Be(provenHeaderToSerialize.GetHash());
                provenHeaderToDeserialize.Coinstake.GetHash().Should().Be(provenHeaderToSerialize.Coinstake.GetHash());
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
            // Create coinstake Tx
            Transaction previousTx = this.network.CreateTransaction();
            previousTx.AddOutput(new TxOut());
            Transaction coinstakeTx = this.network.CreateTransaction();
            coinstakeTx.AddOutput(new TxOut(0, Script.Empty));
            coinstakeTx.AddOutput(new TxOut());
            coinstakeTx.AddInput(previousTx, 0);
            coinstakeTx.IsCoinStake.Should().BeTrue();
            coinstakeTx.IsCoinBase.Should().BeFalse();

            // Create coinbase Tx
            Transaction coinBaseTx = this.network.CreateTransaction();
            coinBaseTx.AddOutput(50, new Script());
            coinBaseTx.AddInput(new TxIn());
            coinBaseTx.IsCoinBase.Should().BeTrue();
            coinBaseTx.IsCoinStake.Should().BeFalse();

            var block = (PosBlock)this.network.CreateBlock();
            block.AddTransaction(coinBaseTx);
            block.AddTransaction(coinstakeTx);

            ProvenBlockHeader provenBlockHeader = this.factory.CreateProvenBlockHeader(block);

            return provenBlockHeader;
        }
    }
}
