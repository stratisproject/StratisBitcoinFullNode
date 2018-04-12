using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.LightWallet.Tests
{
    public class WalletTestsHelpers
    {
        public static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        public static (ConcurrentChain Chain, List<Block> Blocks) GenerateChainAndBlocksWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            var blocks = new List<Block>();
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
                blocks.Add(block);
            }

            return (chain, blocks);
        }

        /// <summary>
        /// Creates a set of chains 'forking' at a specific block height. You can see the left chain as the old one and the right as the new chain.
        /// </summary>
        /// <param name="blockAmount">Amount of blocks on each chain.</param>
        /// <param name="network">The network to use</param>
        /// <param name="forkBlock">The height at which to put the fork.</param>
        /// <returns></returns>
        public static (ConcurrentChain LeftChain, ConcurrentChain RightChain, List<Block> LeftForkBlocks, List<Block> RightForkBlocks)
            GenerateForkedChainAndBlocksWithHeight(int blockAmount, Network network, int forkBlock)
        {
            var rightchain = new ConcurrentChain(network);
            var leftchain = new ConcurrentChain(network);
            var prevBlockHash = rightchain.Genesis.HashBlock;
            var leftForkBlocks = new List<Block>();
            var rightForkBlocks = new List<Block>();

            // build up left fork fully and right fork until forkblock
            uint256 forkBlockPrevHash = null;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = RandomUtils.GetUInt32();
                leftchain.SetTip(block.Header);

                if (leftchain.Height == forkBlock)
                {
                    forkBlockPrevHash = block.GetHash();
                }
                prevBlockHash = block.GetHash();
                leftForkBlocks.Add(block);

                if (rightchain.Height < forkBlock)
                {
                    rightForkBlocks.Add(block);
                    rightchain.SetTip(block.Header);
                }
            }

            // build up the right fork further.
            for (var i = forkBlock; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = forkBlockPrevHash;
                block.Header.Nonce = RandomUtils.GetUInt32();
                rightchain.SetTip(block.Header);
                forkBlockPrevHash = block.GetHash();
                rightForkBlocks.Add(block);
            }

            // if all blocks are on both sides the fork fails.
            if (leftForkBlocks.All(l => rightForkBlocks.Select(r => r.GetHash()).Contains(l.GetHash())))
            {
                throw new InvalidOperationException("No fork created.");
            }

            return (leftchain, rightchain, leftForkBlocks, rightForkBlocks);
        }
    }
}