using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public class PosBlockAssembler : PowBlockAssembler
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly StakeChain stakeChain;

        public PosBlockAssembler(
            ConsensusLoop consensusLoop,
            Network network,
            ConcurrentChain chain,
            MempoolAsyncLock mempoolLock,
            TxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            StakeChain stakeChain,
            ILoggerFactory loggerFactory,
            AssemblerOptions options = null)
            : base(consensusLoop, network, chain, mempoolLock, mempool, dateTimeProvider, loggerFactory, options)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stakeChain = stakeChain;
        }

        public override BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:{4})", nameof(scriptPubKeyIn), nameof(scriptPubKeyIn.Length), scriptPubKeyIn.Length, nameof(fMineWitnessTx), fMineWitnessTx);

            base.CreateNewBlock(scriptPubKeyIn, fMineWitnessTx);

            this.coinbase.Outputs[0].ScriptPubKey = new Script();
            this.coinbase.Outputs[0].Value = Money.Zero;

            PosConsensusValidator posValidator = this.consensusLoop.Validator as PosConsensusValidator;
            Guard.NotNull(posValidator, nameof(posValidator));

            this.logger.LogTrace("(-)");
            return this.pblocktemplate;
        }

        protected override void UpdateHeaders()
        {
            this.logger.LogTrace("()");

            base.UpdateHeaders();

            var stake = new BlockStake(this.pblock);
            this.pblock.Header.Bits = StakeValidator.GetNextTargetRequired(this.stakeChain, this.consensusLoop.Tip, this.network.Consensus, this.options.IsProofOfStake);

            this.logger.LogTrace("(-)");
        }

        protected override void TestBlockValidity()
        {
            this.logger.LogTrace("()");

            //base.TestBlockValidity();

            this.logger.LogTrace("(-)");
        }
    }
}
