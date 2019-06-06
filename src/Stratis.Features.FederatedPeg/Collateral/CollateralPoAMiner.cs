using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.FederatedPeg.Collateral
{
    public class CollateralPoAMiner : PoAMiner
    {
        /// <summary>Prefix used to identify OP_RETURN output with mainchain consensus height commitment.</summary>
        public static readonly byte[] HeightCommitmentOutputPrefixBytes = new byte[] { 121, 13, 6, 253 };

        public CollateralPoAMiner(IConsensusManager consensusManager, IDateTimeProvider dateTimeProvider, Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState, BlockDefinition blockDefinition, ISlotsManager slotsManager, IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator, IFederationManager federationManager, IIntegrityValidator integrityValidator, IWalletManager walletManager,
            INodeStats nodeStats, VotingManager votingManager, PoAMinerSettings poAMinerSettings)
            : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager, connectionManager,
            poaHeaderValidator, federationManager, integrityValidator, walletManager,nodeStats, votingManager, poAMinerSettings)
        {
            // TODO consensus rule to check commitment is correct
        }

        /// <inheritdoc />
        protected override void FillBlockTemplate(BlockTemplate blockTemplate)
        {
            base.FillBlockTemplate(blockTemplate);

            // TODO add commitment
        }
    }
}
