using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPowContextualRuleTest2 : TestConsensusRulesUnitTestBase
    {
        public BlockHeaderPowContextualRuleTest2() : base()
        {
            this.network = Network.TestNet; //important for bips
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetLower_ThrowsBadDiffBitsConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BestBlock = new ContextBlockInformation()
                    {
                        Height = 5
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111114);

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadDiffBits.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadDiffBits.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetHigher_ThrowsBadDiffBitsConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BestBlock = new ContextBlockInformation()
                    {
                        Height = 5
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111116);

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadDiffBits.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadDiffBits.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldLower_ThrowsTimeTooOldConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        Height = 5
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 9));

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.TimeTooOld.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.TimeTooOld.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldEqual_ThrowsTimeTooOldConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        Height = 5
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.TimeTooOld.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.TimeTooOld.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_TimeTooNew_ThrowsTimeTooNewConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2016, 12, 31, 10, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        Height = 5
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.TimeTooNew.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.TimeTooNew.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip34_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height above bip34
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34]
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 1;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip34_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height same as bip34
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 1
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 1;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightHigherThanBip66_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height above bip66
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 2;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightSameAsBip66_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height same as bip66
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 2;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip66_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height above bip66
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 1;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip66_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height same as bip66
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 1;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan4_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height above bip65
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65]
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 3;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan4_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height same as bip65
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 1
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 3;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }



        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height above bip65
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65]
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 2;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height same as bip65
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 1
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 2;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height above bip65
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65]
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 1;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    Time = new DateTime(2017, 1, 1, 0, 0, 0),
                    BestBlock = new ContextBlockInformation()
                    {
                        MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                        // set height same as bip65
                        Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 1
                    },
                    NextWorkRequired = new Target(0x1f111115),
                    BlockValidationContext = new BlockValidationContext() { Block = new Block() }
                };
                ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
                ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
                ruleContext.BlockValidationContext.Block.Header.Version = 1;

                var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BadVersion.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BadVersion.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip34_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                Time = new DateTime(2017, 1, 1, 0, 0, 0),
                BestBlock = new ContextBlockInformation()
                {
                    MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                    // set height lower than bip34
                    Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 2
                },
                NextWorkRequired = new Target(0x1f111115),
                BlockValidationContext = new BlockValidationContext() { Block = new Block() }
            };
            ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            ruleContext.BlockValidationContext.Block.Header.Version = 1;

            var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip66_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                Time = new DateTime(2017, 1, 1, 0, 0, 0),
                BestBlock = new ContextBlockInformation()
                {
                    MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                    // set height lower than bip66
                    Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 2
                },
                NextWorkRequired = new Target(0x1f111115),
                BlockValidationContext = new BlockValidationContext() { Block = new Block() }
            };
            ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            ruleContext.BlockValidationContext.Block.Header.Version = 2;

            var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip65_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                Time = new DateTime(2017, 1, 1, 0, 0, 0),
                BestBlock = new ContextBlockInformation()
                {
                    MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                    // set height lower than bip365
                    Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 2
                },
                NextWorkRequired = new Target(0x1f111115),
                BlockValidationContext = new BlockValidationContext() { Block = new Block() }
            };
            ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            ruleContext.BlockValidationContext.Block.Header.Version = 3;

            var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionAboveBIPS_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                Time = new DateTime(2017, 1, 1, 0, 0, 0),
                BestBlock = new ContextBlockInformation()
                {
                    MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                    // set height higher than bip65
                    Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] + 30
                },
                NextWorkRequired = new Target(0x1f111115),
                BlockValidationContext = new BlockValidationContext() { Block = new Block() }
            };
            ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            ruleContext.BlockValidationContext.Block.Header.Version = 4;

            var rule = this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>();

            await rule.RunAsync(ruleContext);
        }
    }
}
