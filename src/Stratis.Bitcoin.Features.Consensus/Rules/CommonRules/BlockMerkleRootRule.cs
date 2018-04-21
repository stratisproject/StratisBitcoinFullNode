using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// This rule will validate that the calculated merkle tree matches the merkle root in the header.
    /// </summary>
    /// <remarks>
    /// Transactions in a block are hashed together using SHA256 in to a merkel tree, 
    /// the root of that tree is included in the block header.
    /// </remarks>
    /// <remarks>
    /// Check for merkle tree malleability (CVE-2012-2459): repeating sequences
    /// of transactions in a block without affecting the merkle root of a block,
    /// while still invalidating it.
    /// </remarks>    
    public class BlockMerkleRootRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadMerkleRoot">The block merkle root is different from the computed merkle root.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionDuplicate">One of the leaf nodes on the merkle tree has a duplicate hash within the subtree.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.CheckMerkleRoot)
            {
                Block block = context.BlockValidationContext.Block;

                uint256 hashMerkleRoot2 = BlockMerkleRoot(block, out bool mutated);
                if (block.Header.HashMerkleRoot != hashMerkleRoot2)
                {
                    this.Logger.LogTrace("(-)[BAD_MERKLE_ROOT]");
                    ConsensusErrors.BadMerkleRoot.Throw();
                }

                if (mutated)
                {
                    this.Logger.LogTrace("(-)[BAD_TX_DUP]");
                    ConsensusErrors.BadTransactionDuplicate.Throw();
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Calculates merkle root for block's transactions.
        /// </summary>
        /// <param name="block">Block which transactions are used for calculation.</param>
        /// <param name="mutated"><c>true</c> if block contains repeating sequences of transactions without affecting the merkle root of a block. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        public static uint256 BlockMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>(block.Transactions.Count);
            foreach (Transaction tx in block.Transactions)
                leaves.Add(tx.GetHash());

            return ComputeMerkleRoot(leaves, out mutated);
        }

        /// <summary>
        /// Computes merkle root.
        /// </summary>
        /// <remarks>This implements a constant-space merkle root/path calculator, limited to 2^32 leaves.</remarks>
        /// <param name="leaves">Merkle tree leaves.</param>
        /// <param name="mutated"><c>true</c> if at least one leaf of the merkle tree has the same hash as any subtree. Otherwise: <c>false</c>.</param>
        public static uint256 ComputeMerkleRoot(List<uint256> leaves, out bool mutated)
        {
            var branch = new List<uint256>();

            mutated = false;
            if (leaves.Count == 0)
                return uint256.Zero;

            // count is the number of leaves processed so far.
            uint count = 0;

            // inner is an array of eagerly computed subtree hashes, indexed by tree
            // level (0 being the leaves).
            // For example, when count is 25 (11001 in binary), inner[4] is the hash of
            // the first 16 leaves, inner[3] of the next 8 leaves, and inner[0] equal to
            // the last leaf. The other inner entries are undefined.
            var inner = new uint256[32];

            for (int i = 0; i < inner.Length; i++)
                inner[i] = uint256.Zero;

            // Which position in inner is a hash that depends on the matching leaf.
            int matchLevel = -1;

            // First process all leaves into 'inner' values.
            while (count < leaves.Count)
            {
                uint256 h = leaves[(int)count];
                bool match = false;
                count++;
                int level;

                // For each of the lower bits in count that are 0, do 1 step. Each
                // corresponds to an inner value that existed before processing the
                // current leaf, and each needs a hash to combine it.
                for (level = 0; (count & (((uint)1) << level)) == 0; level++)
                {
                    if (branch != null)
                    {
                        if (match)
                        {
                            branch.Add(inner[level]);
                        }
                        else if (matchLevel == level)
                        {
                            branch.Add(h);
                            match = true;
                        }
                    }
                    if (!mutated)
                        mutated = inner[level] == h;
                    var hash = new byte[64];
                    Buffer.BlockCopy(inner[level].ToBytes(), 0, hash, 0, 32);
                    Buffer.BlockCopy(h.ToBytes(), 0, hash, 32, 32);
                    h = Hashes.Hash256(hash);
                }

                // Store the resulting hash at inner position level.
                inner[level] = h;
                if (match)
                    matchLevel = level;
            }

            uint256 root;

            {
                // Do a final 'sweep' over the rightmost branch of the tree to process
                // odd levels, and reduce everything to a single top value.
                // Level is the level (counted from the bottom) up to which we've sweeped.
                int level = 0;

                // As long as bit number level in count is zero, skip it. It means there
                // is nothing left at this level.
                while ((count & (((uint)1) << level)) == 0)
                    level++;

                root = inner[level];
                bool match = matchLevel == level;
                while (count != (((uint)1) << level))
                {
                    // If we reach this point, h is an inner value that is not the top.
                    // We combine it with itself (Bitcoin's special rule for odd levels in
                    // the tree) to produce a higher level one.
                    if (match)
                        branch.Add(root);

                    var hash = new byte[64];
                    Buffer.BlockCopy(root.ToBytes(), 0, hash, 0, 32);
                    Buffer.BlockCopy(root.ToBytes(), 0, hash, 32, 32);
                    root = Hashes.Hash256(hash);

                    // Increment count to the value it would have if two entries at this
                    // level had existed.
                    count += (((uint)1) << level);
                    level++;

                    // And propagate the result upwards accordingly.
                    while ((count & (((uint)1) << level)) == 0)
                    {
                        if (match)
                        {
                            branch.Add(inner[level]);
                        }
                        else if (matchLevel == level)
                        {
                            branch.Add(root);
                            match = true;
                        }

                        var hashh = new byte[64];
                        Buffer.BlockCopy(inner[level].ToBytes(), 0, hashh, 0, 32);
                        Buffer.BlockCopy(root.ToBytes(), 0, hashh, 32, 32);
                        root = Hashes.Hash256(hashh);

                        level++;
                    }
                }
            }

            return root;
        }
    }
}