using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.BlockStore.AddressIndexing;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Collateral
{
    /// <summary>
    /// Collateral aware version of <see cref="PoAMiner"/>. At the block template creation it will check our own collateral at a commitment height which is
    /// calculated in a following way: <c>counter chain height - maxReorgLength - AddressIndexer.SyncBuffer</c>. Then commitment height is encoded in
    /// OP_RETURN output of a coinbase transaction.
    /// </summary>
    public class CollateralPoAMiner : PoAMiner
    {
        private readonly CollateralHeightCommitmentEncoder encoder;

        private readonly ICollateralChecker collateralChecker;

        public CollateralPoAMiner(IConsensusManager consensusManager, IDateTimeProvider dateTimeProvider, Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState, BlockDefinition blockDefinition, ISlotsManager slotsManager, IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator, IFederationManager federationManager, IIntegrityValidator integrityValidator, IWalletManager walletManager,
            INodeStats nodeStats, VotingManager votingManager, PoAMinerSettings poAMinerSettings, ICollateralChecker collateralChecker, IAsyncProvider asyncProvider)
            : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager, connectionManager,
            poaHeaderValidator, federationManager, integrityValidator, walletManager,nodeStats, votingManager, poAMinerSettings, asyncProvider)
        {
            this.collateralChecker = collateralChecker;
            this.encoder = new CollateralHeightCommitmentEncoder();
        }

        /// <inheritdoc />
        protected override void FillBlockTemplate(BlockTemplate blockTemplate, out bool dropTemplate)
        {
            base.FillBlockTemplate(blockTemplate, out dropTemplate);

            int counterChainHeight = this.collateralChecker.GetCounterChainConsensusHeight();
            int maxReorgLength = AddressIndexer.GetMaxReorgOrFallbackMaxReorg(this.network);

            int commitmentHeight = counterChainHeight - maxReorgLength - AddressIndexer.SyncBuffer;

            if (commitmentHeight <= 0)
            {
                dropTemplate = true;
                this.logger.LogWarning("Counter chain should first advance at least at {0}! Block can't bee produced.", maxReorgLength + AddressIndexer.SyncBuffer);
                this.logger.LogTrace("(-)[LOW_COMMITMENT_HEIGHT]");
                return;
            }

            IFederationMember currentMember = this.federationManager.GetCurrentFederationMember();

            if (currentMember == null)
            {
                dropTemplate = true;
                this.logger.LogWarning("Unable to get this node's federation member!");
                this.logger.LogTrace("(-)[CANT_GET_FED_MEMBER]");
                return;
            }

            // Check our own collateral at a given commitment height.
            bool success = this.collateralChecker.CheckCollateral(currentMember, commitmentHeight);

            if (!success)
            {
                dropTemplate = true;
                this.logger.LogWarning("Failed to fulfill collateral requirement for mining!");
                this.logger.LogTrace("(-)[BAD_COLLATERAL]");
                return;
            }

            // Add height commitment.
            byte[] encodedHeight = this.encoder.EncodeWithPrefix(commitmentHeight);

            var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(encodedHeight));
            blockTemplate.Block.Transactions[0].AddOutput(Money.Zero, votingOutputScript);
        }
    }

    public class CollateralHeightCommitmentEncoder
    {
        /// <summary>Prefix used to identify OP_RETURN output with mainchain consensus height commitment.</summary>
        public static readonly byte[] HeightCommitmentOutputPrefixBytes = { 121, 13, 6, 253 };

        /// <summary>Converts <paramref name="height"/> to a byte array which has a prefix of <see cref="HeightCommitmentOutputPrefixBytes"/>.</summary>
        public byte[] EncodeWithPrefix(int height)
        {
            var bytes = new List<byte>(HeightCommitmentOutputPrefixBytes);

            bytes.AddRange(BitConverter.GetBytes(height));

            return bytes.ToArray();
        }

        /// <summary>Provides commitment data from transaction's coinbase height commitment output.</summary>
        /// <returns>Commitment script or <c>null</c> if commitment script wasn't found.</returns>
        public byte[] ExtractRawCommitmentData(Transaction tx)
        {
            IEnumerable<Script> opReturnOutputs = tx.Outputs.Where(x => (x.ScriptPubKey.Length > 0) && (x.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN)).Select(x => x.ScriptPubKey);

            byte[] commitmentData = null;

            foreach (Script script in opReturnOutputs)
            {
                IEnumerable<Op> ops = script.ToOps();

                if (ops.Count() != 2)
                    continue;

                byte[] data = ops.Last().PushData;

                bool correctPrefix = data.Take(HeightCommitmentOutputPrefixBytes.Length).SequenceEqual(HeightCommitmentOutputPrefixBytes);

                if (!correctPrefix)
                    continue;

                commitmentData = data.Skip(HeightCommitmentOutputPrefixBytes.Length).ToArray();
                break;
            }

            return commitmentData;
        }

        public int Decode(byte[] rawData)
        {
            int height = BitConverter.ToInt32(rawData);

            return height;
        }
    }
}
