using System.Linq;

namespace NBitcoin
{
    /// <summary>
    /// Extension to an existing <see cref="BlockHeader"/> which is used in PoS to prevent attacker from constructing
    /// a fake chain of headers that has more work than the valid chain and attacking a node.
    ///
    /// Proven header prevents such an attack by including additional information that can be validated and confirmed whether
    /// the header is fake or real.
    ///
    /// Additional information included into proven header:
    ///
    /// Block header signature (<see cref="Signature"/>), which is signed with the private key which corresponds to
    /// coinstake's Second output's public key.
    ///
    /// Coinstake transaction (<see cref="Coinstake"/>).
    ///
    /// Merkle proof (<see cref="MerkleProof"/>) that proves the coinstake tx is included in a block that is being represented by the provided header.
    /// </summary>
    public class ProvenBlockHeader : PosBlockHeader
    {
        public ProvenBlockHeader(PosBlock block)
        {
            this.signature = block.BlockSignature;

            uint256[] txIds = block.Transactions.Select(t => t.GetHash()).ToArray();
            this.merkleProof = block.Filter(txIds).PartialMerkleTree;

            this.coinstake = block.Transactions[1];
        }

        /// <summary>
        /// Merkle proof that proves the coinstake tx is included in a block that is being represented by the provided header.
        /// </summary>
        private PartialMerkleTree merkleProof;

        /// <summary>
        /// Gets merkle proof that proves the coinstake tx is included in a block that is being represented by the provided header.
        /// </summary>
        public PartialMerkleTree MerkleProof => this.merkleProof;

        /// <summary>
        /// Block header signature which is signed with the private key which corresponds to
        /// coinstake's Second output's public key.
        /// </summary>
        private BlockSignature signature;

        /// <summary>
        /// Gets block header signature which is signed with the private key which corresponds to
        /// coinstake's Second output's public key.
        /// </summary>
        public BlockSignature Signature => this.signature;

        /// <summary>
        /// Coinstake transaction.
        /// </summary>
        private Transaction coinstake;

        /// <summary>
        /// Gets coinstake transaction
        /// </summary>
        public Transaction Coinstake => this.coinstake;

        /// <inheritdoc />
        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);
            stream.ReadWrite(ref this.merkleProof);
            stream.ReadWrite(ref this.signature);
            stream.ReadWrite(ref this.coinstake);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.GetHash().ToString();
        }
    }
}
