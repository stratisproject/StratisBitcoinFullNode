using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    /// <summary>
    /// Creates different types of valid and invalid blocks.
    /// </summary>
    public sealed class BlockBuilder
    {
        private int amountOfBlocks;
        private int invalidBlockHeight;
        private CoreNode coreNode;
        private Func<CoreNode, Block, Block> invalidBlock;

        /// <summary>
        /// The node the blocks will be created on.
        /// </summary>
        public BlockBuilder OnNode(CoreNode coreNode)
        {
            this.coreNode = coreNode;

            TestHelper.SetMinerSecret(this.coreNode);

            return this;
        }

        /// <summary>
        /// Creates an invalid block at the specified height.
        /// </summary>
        /// <param name="invalidBlockHeight">The height at which the invalid block should be created.</param>
        /// <param name="invalidBlock">The type of invalid block to create.</param>
        public BlockBuilder Invalid(int invalidBlockHeight, Func<CoreNode, Block, Block> invalidBlock)
        {
            this.invalidBlockHeight = invalidBlockHeight;
            this.invalidBlock = invalidBlock;
            return this;
        }

        /// <summary>Sets the amount of valid blocks to create.</summary>
        public BlockBuilder Amount(int amountOfBlocks)
        {
            this.amountOfBlocks = amountOfBlocks;
            return this;
        }

        /// <summary>
        /// Constructs a chain of valid and if so specified and invalid block at a given height.
        /// <para>
        /// Each block will also be submitted to consensus.
        /// </para>
        /// </summary>
        public async Task<ChainedHeader> BuildAsync()
        {
            var chainTip = this.coreNode.FullNode.ConsensusManager().Tip;
            var chainTipHeight = chainTip.Height;

            var dateTimeProvider = this.coreNode.FullNode.NodeService<IDateTimeProvider>();

            for (int height = chainTipHeight + 1; height <= chainTipHeight + this.amountOfBlocks; height++)
            {
                // Create the block and set the header properties.
                var block = this.coreNode.FullNode.Network.CreateBlock();
                block.Header.HashPrevBlock = chainTip.HashBlock;
                block.Header.UpdateTime(dateTimeProvider.GetTimeOffset(), this.coreNode.FullNode.Network, chainTip);
                block.Header.Bits = block.Header.GetWorkRequired(this.coreNode.FullNode.Network, chainTip);

                // Create a valid coinbase transaction.
                var coinbase = this.coreNode.FullNode.Network.CreateTransaction();
                coinbase.Time = (uint)dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
                coinbase.AddInput(TxIn.CreateCoinbase(chainTip.Height + 1));
                coinbase.AddOutput(new TxOut(this.coreNode.FullNode.Network.Consensus.ProofOfWorkReward, this.coreNode.MinerSecret.GetAddress()));
                block.AddTransaction(coinbase);

                // Check to see whether or not the block should be invalid.
                if (height == this.invalidBlockHeight)
                    block = this.invalidBlock(this.coreNode, block);

                block.UpdateMerkleRoot();

                uint nonce = 0;
                while (!block.CheckProofOfWork())
                    block.Header.Nonce = ++nonce;

                // This will set the block's BlockSize property.
                block = Block.Load(block.ToBytes(), this.coreNode.FullNode.Network);

                // Submit the block to consensus so that the chain's tip can be updated.
                await this.coreNode.FullNode.NodeService<IConsensusManager>().BlockMinedAsync(block).ConfigureAwait(false);

                chainTip = this.coreNode.FullNode.ConsensusManager().Tip;
            }

            return chainTip;
        }

        /// <summary>
        /// Produces a block that will fail Full Validation due to an invalid coinbase reward.
        /// </summary>
        public static Block InvalidCoinbaseReward(CoreNode coreNode, Block block)
        {
            block.Transactions[0].Outputs[0].Value = Money.Coins(999);
            return block;
        }


        /// <summary>
        /// Produces a block that will fail Partial Validation due to it containing a duplicate coinbase transction.
        /// </summary>
        public static Block InvalidDuplicateCoinbase(CoreNode coreNode, Block block)
        {
            var badTxNoInputs = coreNode.FullNode.Network.CreateTransaction();
            badTxNoInputs.Time = (uint)coreNode.FullNode.NodeService<IDateTimeProvider>().GetAdjustedTimeAsUnixTimestamp();
            badTxNoInputs.AddInput(new TxIn());
            badTxNoInputs.AddOutput(new TxOut(Money.Coins(1), coreNode.MinerSecret.GetAddress()));

            block.AddTransaction(badTxNoInputs);

            return block;
        }
    }
}