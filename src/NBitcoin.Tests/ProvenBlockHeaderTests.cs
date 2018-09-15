using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace NBitcoin.Tests
{
    public class ProvenBlockHeaderTests
    {
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
                provenHeaderToDeserialize.ReadWrite(bytes, this.network.Consensus.ConsensusFactory);

                provenHeaderToDeserialize.GetHash().Should().Be(provenHeaderToSerialize.GetHash());

                // Check if merke proofs are identical.
                provenHeaderToDeserialize.MerkleProof.Hashes.Should().BeEquivalentTo(provenHeaderToSerialize.MerkleProof.Hashes);
                provenHeaderToDeserialize.MerkleProof.TransactionCount.Should().Be(provenHeaderToSerialize.MerkleProof.TransactionCount);
                for (int i = 0; i < provenHeaderToSerialize.MerkleProof.Flags.Length; i++)
                {
                    provenHeaderToDeserialize.MerkleProof.Flags[i].Should().Be(provenHeaderToSerialize.MerkleProof.Flags[i]);
                }

                // Check if coinstake properties match.
                provenHeaderToDeserialize.Coinstake.Should().BeEquivalentTo(provenHeaderToSerialize.Coinstake);

                // Check if signature properties match.
                provenHeaderToDeserialize.Signature.Signature.Should().BeEquivalentTo(provenHeaderToSerialize.Signature.Signature);

                // Check base properties.
                provenHeaderToDeserialize.BlockTime.Should().Be(provenHeaderToSerialize.BlockTime);
                provenHeaderToDeserialize.CurrentVersion.Should().Be(provenHeaderToSerialize.CurrentVersion);
                provenHeaderToDeserialize.Nonce.Should().Be(provenHeaderToSerialize.Nonce);
                provenHeaderToDeserialize.Time.Should().Be(provenHeaderToSerialize.Time);
                provenHeaderToDeserialize.Version.Should().Be(provenHeaderToSerialize.Version);
            }
        }

        [Fact]
        public void ShouldNotBeAbleToCreateProvenBlockHeaderFromANullBlock()
        {
            Action createProvenHeader = () => ((PosConsensusFactory)this.network.Consensus.ConsensusFactory).CreateProvenBlockHeader(null);
            createProvenHeader.Should().Throw<ArgumentNullException>();
        }
        
        [Fact]
        public void WhenCreatingNewProvenHeaderMerkleProofIsCorrectlyCreated()
        {
            PosBlock block = this.CreatePosBlockMock();

            // Add 20 more transactions.
            for (int i = 0; i < 20; i++)
            {
                Transaction tx = this.network.CreateTransaction();

                tx.AddInput(new TxIn(Script.Empty));
                tx.AddOutput(Money.COIN + i, new Script(Enumerable.Range(1, 5).SelectMany(index => Guid.NewGuid().ToByteArray())));

                block.AddTransaction(tx);
            }

            block.UpdateMerkleRoot();
            ProvenBlockHeader provenBlockHeader = ((PosConsensusFactory)this.network.Consensus.ConsensusFactory).CreateProvenBlockHeader(block);
            provenBlockHeader.MerkleProof.Hashes.Should().HaveCount(6);
            provenBlockHeader.MerkleProof.Check(provenBlockHeader.HashMerkleRoot).Should().BeTrue();
        }

        private ProvenBlockHeader CreateNewProvenBlockHeaderMock()
        {
            PosBlock block = this.CreatePosBlockMock();
            ProvenBlockHeader provenBlockHeader = ((PosConsensusFactory)this.network.Consensus.ConsensusFactory).CreateProvenBlockHeader(block);

            return provenBlockHeader;
        }

        private PosBlock CreatePosBlockMock()
        {
            // Create coinstake Tx.
            Transaction previousTx = this.network.CreateTransaction();
            previousTx.AddOutput(new TxOut());
            Transaction coinstakeTx = this.network.CreateTransaction();
            coinstakeTx.AddOutput(new TxOut(0, Script.Empty));
            coinstakeTx.AddOutput(new TxOut(50, new Script()));
            coinstakeTx.AddInput(previousTx, 0);
            coinstakeTx.IsCoinStake.Should().BeTrue();
            coinstakeTx.IsCoinBase.Should().BeFalse();

            // Create coinbase Tx.
            Transaction coinBaseTx = this.network.CreateTransaction();
            coinBaseTx.AddOutput(100, new Script());
            coinBaseTx.AddInput(new TxIn());
            coinBaseTx.IsCoinBase.Should().BeTrue();
            coinBaseTx.IsCoinStake.Should().BeFalse();

            var block = (PosBlock)this.network.CreateBlock();
            block.AddTransaction(coinBaseTx);
            block.AddTransaction(coinstakeTx);
            block.BlockSignature = new BlockSignature { Signature = new byte[] { 0x2, 0x3 } };

            return block;
        }
    }
}
