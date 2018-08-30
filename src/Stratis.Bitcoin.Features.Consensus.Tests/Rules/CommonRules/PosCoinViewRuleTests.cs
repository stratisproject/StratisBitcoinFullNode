using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    /// <summary>
    /// Test cases related to the <see cref="PosCoinviewRule"/>.
    /// </summary>
    public class PosCoinViewRuleTests : TestPosConsensusRulesUnitTestBase
    {
        public ConsensusManager Consensus { get; private set; }
        public ICoinView CoinView { get; private set; }

        public Dictionary<uint256, UnspentOutputs> UnspentOutputs { get; private set; }

        /// <summary>
        /// Initializes the unspent output set and creates the block to validate.
        /// </summary>
        public PosCoinViewRuleTests():base()
        {
            this.UnspentOutputs = new Dictionary<uint256, UnspentOutputs>();

            (this.ruleContext as UtxoRuleContext).UnspentOutputSet = new UnspentOutputSet();
            (this.ruleContext as UtxoRuleContext).UnspentOutputSet.SetCoins(new UnspentOutputs[0]);

            var genesis = this.network.GetGenesis();
            this.concurrentChain.SetTip(genesis.Header);
            this.concurrentChain.Tip.Block = genesis;

            this.ruleContext.ValidationContext.BlockToValidate = this.network.Consensus.ConsensusFactory.CreateBlock();
        }

        /// <summary>
        /// Creates the consensus manager.
        /// </summary>
        /// <param name="network">The network to construct this for.</param>
        /// <param name="dataDir">The data directory to use.</param>
        private void CreateConsensusManager(Network network, string dataDir)
        {
            var nodeSettings = new Configuration.NodeSettings(network, args: new string[] { $"-datadir={dataDir}" });
            var loggerFactory = nodeSettings.LoggerFactory;
            var consensusSettings = new ConsensusSettings(nodeSettings);
            var checkpoints = new Checkpoints();
            var chainState = new ChainState() { BlockStoreTip = this.concurrentChain.Tip };
            var initialBlockDownloadState = new InitialBlockDownloadState(chainState, network, consensusSettings, new Checkpoints());
            var coinView = new Mock<ICoinView>();
            var deployments = new NodeDeployments(network, this.concurrentChain);
            var stakeChain = new Mock<IStakeChain>();
            var stakeValidator = new Mock<Interfaces.IStakeValidator>();

            // Register POS consensus rules.
            new FullNodeBuilderConsensusExtension.PosConsensusRulesRegistration().RegisterRules(network.Consensus);
            var consensusRules = new PosConsensusRuleEngine(network, loggerFactory, DateTimeProvider.Default,
                this.concurrentChain, deployments, consensusSettings, checkpoints, coinView.Object, stakeChain.Object, stakeValidator.Object, chainState, new InvalidBlockHashStore(new DateTimeProvider()))
                .Register();
            var headerValidator = new HeaderValidator(consensusRules, loggerFactory);
            var integrityValidator = new IntegrityValidator(consensusRules, loggerFactory);
            var partialValidator = new PartialValidator(consensusRules, loggerFactory);

            // Create consensus manager.
            this.Consensus = new ConsensusManager(network, loggerFactory, chainState, headerValidator, integrityValidator,
                partialValidator, checkpoints, consensusSettings, consensusRules, new Mock<IFinalizedBlockInfo>().Object, new Signals.Signals(),
                new Mock<IPeerBanning>().Object, initialBlockDownloadState, this.concurrentChain, new Mock<IBlockPuller>().Object, new Mock<IBlockStore>().Object,
                new InvalidBlockHashStore(new DateTimeProvider()));

            // Mock the coinviews "FetchCoinsAsync" method. We will use a dictionary to track spendable outputs.
            coinView.Setup(d => d.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .Returns((uint256[] txIds, CancellationToken cancel) => Task.Run(() => {

                    var result = new UnspentOutputs[txIds.Length];

                    for (int i = 0; i < txIds.Length; i++)
                        result[i] = this.UnspentOutputs.TryGetValue(txIds[i], out UnspentOutputs unspentOutputs) ? unspentOutputs : null;

                    return new FetchCoinsResponse(result, this.concurrentChain.Tip.HashBlock);
                }));

            // Mock the coinviws "GetTipHashAsync" method.
            coinView.Setup(d => d.GetTipHashAsync(It.IsAny<CancellationToken>())).Returns(() => Task.Run(() => {
                return this.concurrentChain.Tip.HashBlock;
            }));

            // Since we are mocking the stake validator ensure that GetNextTargetRequired returns something sensible. Otherwise we get the "bad-diffbits" error.
            stakeValidator.Setup(s => s.GetNextTargetRequired(It.IsAny<IStakeChain>(), It.IsAny<ChainedHeader>(), It.IsAny<IConsensus>(), It.IsAny<bool>())).Returns(network.Consensus.PowLimit);

            // Since we are mocking the stakechain ensure that the Get returns a BlockStake. Otherwise this results in "previous stake is not found".
            stakeChain.Setup(d => d.Get(It.IsAny<uint256>())).Returns(new BlockStake()
            {
                Flags = BlockFlag.BLOCK_PROOF_OF_STAKE,
                StakeModifierV2 = 0,
                StakeTime = (this.concurrentChain.Tip.Header.Time + 60) & ~PosTimeMaskRule.StakeTimestampMask
            });

            // Initialize the consensus manager.
            this.Consensus.InitializeAsync(this.concurrentChain.Tip).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Tests whether an error is raised when a miner attempts to stake an output which he can't spend with his private key.
        /// </summary>
        /// <description>
        /// Create a proof of stake block with coinstake transaction which will have two inputs. The kernel input will be from the
        /// received coin. But the second input will be from the change address of the first miner and because the second miner
        /// does not have a private key for that coin this input will be signed with invalid signature. There will be just two outputs,
        /// first is the coinstake marker and the second is normal pay to public key that belongs to the second miner with value that
        /// equals to the sum of the inputs.
        /// The testable outcome is whether the first miner accepts such a block. Obviously, the test should fail if the block is accepted.
        /// </description>
        [Fact]
        public async Task PosCoinViewRuleFailsAsync()
        {
            this.CreateConsensusManager(this.network, TestBase.GetTestDirectoryPath(this));

            // Keys used by the miner and recipient.
            var minerKey1 = new Key();
            var minerKey2 = new Key();

            // The scriptPubKeys if the miner and recipient.
            Script scriptPubKey1 = minerKey1.PubKey.ScriptPubKey;
            Script scriptPubKey2 = minerKey2.PubKey.ScriptPubKey;

            // Add dummy first transaction.
            Transaction transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(TxIn.CreateCoinbase(this.concurrentChain.Tip.Height + 1));
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            Assert.True(transaction.IsCoinBase);

            // Add first transaction to block.
            Block block = this.ruleContext.ValidationContext.BlockToValidate;
            block.Transactions.Add(transaction);

            // Create a previous transaction with scriptPubKey outputs.
            Transaction prevTransaction = this.network.CreateTransaction();
            // Received coin.
            prevTransaction.Outputs.Add(new TxOut(Money.COIN * 5_000_000, scriptPubKey2));
            // Change address.
            prevTransaction.Outputs.Add(new TxOut(Money.COIN * 10_000_000, scriptPubKey1));

            // Record the spendable outputs.
            this.UnspentOutputs[prevTransaction.GetHash()] = new UnspentOutputs(1, prevTransaction);

            // Create coin stake transaction.
            Transaction coinstakeTransaction = this.network.CreateTransaction();

            coinstakeTransaction.Inputs.Add(new TxIn(new OutPoint(prevTransaction, 0)));
            coinstakeTransaction.Inputs.Add(new TxIn(new OutPoint(prevTransaction, 1)));

            // Coinstake marker.
            coinstakeTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            // Normal pay to public key that belongs to the second miner with value that
            // equals to the sum of the inputs.
            coinstakeTransaction.Outputs.Add(new TxOut(Money.COIN * 10_000_000, scriptPubKey2));

            // Sign the transaction inputs.
            new TransactionBuilder(this.network)
                .AddKeys(minerKey2)
                .AddCoins(new Coin(new OutPoint(prevTransaction, 0), prevTransaction.Outputs[0]))
                .SignTransactionInPlace(coinstakeTransaction);

            Assert.True(coinstakeTransaction.IsCoinStake);

            // Add second transaction to block.
            block.Transactions.Add(coinstakeTransaction);

            // Finalize the block and add it to the chain.
            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Time = (this.concurrentChain.Tip.Header.Time + 60) & ~PosTimeMaskRule.StakeTimestampMask;
            block.Header.Bits = block.Header.GetWorkRequired(this.network, this.concurrentChain.Tip);
            block.SetPrivatePropertyValue("BlockSize", 1L);
            block.Transactions[0].Time = block.Header.Time;
            block.Transactions[1].Time = block.Header.Time;
            block.UpdateMerkleRoot();
            Assert.True(BlockStake.IsProofOfStake(block));
            // Add a signature to the block.
            ECDSASignature signature = minerKey2.Sign(block.GetHash());
            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            // Execute the rule and check the outcome against what is expected.
            var error = await Assert.ThrowsAsync<ConsensusException>(async () => await this.Consensus.BlockMinedAsync(block));
            Assert.Equal(ConsensusErrors.BadTransactionScriptError.Message, error.Message);
        }
    }
}
