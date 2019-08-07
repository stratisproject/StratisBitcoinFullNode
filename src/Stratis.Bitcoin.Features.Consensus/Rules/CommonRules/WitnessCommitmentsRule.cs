using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>A rule that validates witness commitments.</summary>
    public class WitnessCommitmentsRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadWitnessNonceSize">The witness nonce size is invalid.</exception>
        /// <exception cref="ConsensusErrors.BadWitnessMerkleMatch">The witness merkle commitment does not match the computed commitment.</exception>
        /// <exception cref="ConsensusErrors.UnexpectedWitness">The block does not expect witness transactions but contains a witness transaction.</exception>
        public override Task RunAsync(RuleContext context)
        {
            return this.ValidateWitnessCommitment(context, this.Parent.Network);
        }

        /// <summary>
        /// Validation of the witness commitment if its found.
        /// </summary>
        public Task ValidateWitnessCommitment(RuleContext context, Network network)
        { 
            if (context.SkipValidation)
                return Task.CompletedTask;

            DeploymentFlags deploymentFlags = context.Flags;
            Block block = context.ValidationContext.BlockToValidate;

            // Validation for witness commitments.
            // * We compute the witness hash (which is the hash including witnesses) of all the block's transactions, except the
            //   coinbase (where 0x0000....0000 is used instead).
            // * The coinbase scriptWitness is a stack of a single 32-byte vector, containing a witness nonce (unconstrained).
            // * We build a merkle tree with all those witness hashes as leaves (similar to the hashMerkleRoot in the block header).
            // * There must be at least one output whose scriptPubKey is a single 36-byte push, the first 4 bytes of which are
            //   {0xaa, 0x21, 0xa9, 0xed}, and the following 32 bytes are SHA256^2(witness root, witness nonce). In case there are
            //   multiple, the last one is used.
            bool fHaveWitness = false;
            if (deploymentFlags.ScriptFlags.HasFlag(ScriptVerify.Witness))
            {
                Script commitment = GetWitnessCommitment(network, block);
                if (commitment != null)
                {
                    uint256 hashWitness = BlockWitnessMerkleRoot(block, out bool _);

                    // The malleation check is ignored; as the transaction tree itself
                    // already does not permit it, it is impossible to trigger in the
                    // witness tree.
                    WitScript witness = block.Transactions[0].Inputs[0].WitScript;
                    if ((witness.PushCount != 1) || (witness.Pushes.First().Length != 32))
                    {
                        // Witness information is missing, activating witness requirement for peers is required.
                        context.ValidationContext.MissingServices = NetworkPeerServices.NODE_WITNESS;

                        this.Logger.LogTrace("(-)[BAD_WITNESS_NONCE_SIZE]");
                        ConsensusErrors.BadWitnessNonceSize.Throw();
                    }

                    var hashed = new byte[64];
                    Buffer.BlockCopy(hashWitness.ToBytes(), 0, hashed, 0, 32);
                    Buffer.BlockCopy(witness.Pushes.First(), 0, hashed, 32, 32);
                    hashWitness = Hashes.Hash256(hashed);

                    if (!this.EqualsArray(hashWitness.ToBytes(), commitment.ToBytes(true).Skip(6).ToArray(), 32))
                    {
                        this.Logger.LogTrace("(-)[WITNESS_MERKLE_MISMATCH]");
                        ConsensusErrors.BadWitnessMerkleMatch.Throw();
                    }

                    fHaveWitness = true;
                }
            }

            if (!fHaveWitness)
            {
                for (int i = 0; i < block.Transactions.Count; i++)
                {
                    if (block.Transactions[i].HasWitness)
                    {
                        this.Logger.LogTrace("(-)[UNEXPECTED_WITNESS]");
                        ConsensusErrors.UnexpectedWitness.Throw();
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if first <paramref name="length"/> entries are equal between two arrays.
        /// </summary>
        /// <param name="a">First array.</param>
        /// <param name="b">Second array.</param>
        /// <param name="length">Number of entries to be checked.</param>
        /// <returns><c>true</c> if <paramref name="length"/> entries are equal between two arrays. Otherwise <c>false</c>.</returns>
        private bool EqualsArray(byte[] a, byte[] b, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets commitment in the last coinbase transaction output with SegWit flag.
        /// </summary>
        /// <param name="block">Block which coinbase transaction's outputs will be checked for SegWit flags.</param>
        /// <returns>
        /// <c>null</c> if no SegWit flags were found.
        /// If SegWit flag is found the commitment of the last transaction's output that has SegWit flag is returned.
        /// </returns>
        public static Script GetWitnessCommitment(Network network, Block block)
        {
            Script commitScriptPubKey = null;

            if (network.Consensus.IsProofOfStake && network.Consensus.PosEmptyCoinbase)
            {
                Script scriptSig = block.Transactions[0].Inputs[0].ScriptSig;

                var ops = scriptSig.ToOps();

                if (ops.Count > 2 && IsWitnessScript(new Script(ops.Skip(2))))
                {
                    // We assume the first two ops are the BIP34 coinbase height.
                    commitScriptPubKey = new Script(ops.Skip(2));
                }
            }
            else
            {
                for (int i = 0; i < block.Transactions[0].Outputs.Count; i++)
                {
                    Script scriptPubKey = block.Transactions[0].Outputs[i].ScriptPubKey;

                    if (IsWitnessScript(scriptPubKey))
                    {
                        commitScriptPubKey = scriptPubKey;
                    }
                }
            }

            return commitScriptPubKey;
        }

        /// <summary>
        /// Clear all witness commitments from the block.
        /// </summary>
        public static void ClearWitnessCommitment(Network network, Block block)
        {
            if (network.Consensus.IsProofOfStake && network.Consensus.PosEmptyCoinbase)
            {
                Script scriptSig = block.Transactions[0].Inputs[0].ScriptSig;

                var ops = scriptSig.ToOps();

                if (ops.Count > 2 && IsWitnessScript(new Script(ops.Skip(2))))
                {
                    block.Transactions[0].Inputs[0].ScriptSig = new Script(ops.Take(2));
                }
            }
            else
            {
                for (int i = 0; i < block.Transactions[0].Outputs.Count; i++)
                {
                    Script scriptPubKey = block.Transactions[0].Outputs[i].ScriptPubKey;

                    if (IsWitnessScript(scriptPubKey))
                    {
                        block.Transactions[0].Outputs.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        /// <summary>
        /// Create a witness commitment based from the given block.
        /// </summary>
        public static void CreateWitnessCommitment(Network network, Block block)
        {
            var wtxidCoinbase = new byte[32];       // The wtxid of the coinbase transaction is defined as to be 0x0000....0000.
            block.Transactions[0].Inputs[0].WitScript = new WitScript(Op.GetPushOp(wtxidCoinbase));

            // A witness root hash is calculated with all those wtxid as leaves, in a way similar to the hashMerkleRoot in the block header.
            byte[] witnessRootHash = WitnessCommitmentsRule.BlockWitnessMerkleRoot(block, out var _).ToBytes();

            // // Coinbase's input's witness must consist of a single 32-byte array for the witness reserved value.
            byte[] witnessReservedValue = new byte[32];

            byte[] dataToHash = new byte[64]; // witness root hash|witness reserved value
            Buffer.BlockCopy(witnessRootHash, 0, dataToHash, 0, 32);
            Buffer.BlockCopy(witnessReservedValue, 0, dataToHash, 32, 32);

            // 32-byte - Commitment hash: Double-SHA256(witness root hash|witness reserved value)
            byte[] commitmentHash = Hashes.Hash256(dataToHash).ToBytes();

            // The commitment is recorded in a scriptPubKey of the coinbase transaction.
            var coinbaseScriptPubKeyFiledBytes = new byte[38];   // It must be at least 38 bytes, with the first 6-byte of 0x6a24aa21a9ed.
            coinbaseScriptPubKeyFiledBytes[0] = 0x6a;            // OP_RETURN (0x6a)
            coinbaseScriptPubKeyFiledBytes[1] = 0x24;            // Push the following 36 bytes (0x24)
            coinbaseScriptPubKeyFiledBytes[2] = 0xaa;            // Commitment header (0xaa21a9ed)
            coinbaseScriptPubKeyFiledBytes[3] = 0x21;
            coinbaseScriptPubKeyFiledBytes[4] = 0xa9;
            coinbaseScriptPubKeyFiledBytes[5] = 0xed;
            Buffer.BlockCopy(commitmentHash, 0, coinbaseScriptPubKeyFiledBytes, 6, 32);


            if (network.Consensus.IsProofOfStake && network.Consensus.PosEmptyCoinbase)
            {
                // For legacy POS networks where the coinbase is mandatory to be empty
                // we store the witness commitment in the inputs script signature.
                // However as per BIP34 this place is reserved for the block height.
                // In this case we store the commitment after the block height op (push_op + op_0).

                var currentScriptSig = block.Transactions[0].Inputs[0].ScriptSig;

                // If this is a POS network and coinbase limitation we use a coinbase input.
                var commitScript = new Script(currentScriptSig.ToBytes().Concat(coinbaseScriptPubKeyFiledBytes));

                // If there are more than one scriptPubKey matching the pattern, the one with highest output index is assumed to be the commitment.
                block.Transactions[0].Inputs[0].ScriptSig = commitScript;
            }
            else
            {
                // Write the coinbase commitment to a ScriptPubKey structure.
                var txOut = new TxOut(Money.Zero, new Script(coinbaseScriptPubKeyFiledBytes));

                // If there are more than one scriptPubKey matching the pattern, the one with highest output index is assumed to be the commitment.
                block.Transactions[0].Outputs.Add(txOut);
            }
        }

        private static bool IsWitnessScript(Script script)
        {
            if (script.Length >= 38)
            {
                byte[] scriptBytes = script.ToBytes(true);

                if ((scriptBytes[0] == (byte)OpcodeType.OP_RETURN) &&
                    (scriptBytes[1] == 0x24) &&
                    (scriptBytes[2] == 0xaa) &&
                    (scriptBytes[3] == 0x21) &&
                    (scriptBytes[4] == 0xa9) &&
                    (scriptBytes[5] == 0xed))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculates merkle root for witness data.
        /// </summary>
        /// <param name="block">Block which transactions witness data is used for calculation.</param>
        /// <param name="mutated"><c>true</c> if at least one leaf of the merkle tree has the same hash as any subtree. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        public static uint256 BlockWitnessMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>();
            leaves.Add(uint256.Zero); // The witness hash of the coinbase is 0.
            foreach (Transaction tx in block.Transactions.Skip(1))
                leaves.Add(tx.GetWitHash());

            return BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);
        }
    }
}