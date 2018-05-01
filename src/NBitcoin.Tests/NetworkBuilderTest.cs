using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Xunit;

namespace NBitcoin.Tests
{
    public class NetworkBuilderTest
    {
        private NetworkBuilder builder;

        public NetworkBuilderTest()
        {
            this.builder = new NetworkBuilder();
        }

        [Fact]
        public void SetName_SetsNetworkNameOnBuilder()
        {
            this.builder.SetName("MyNetwork");

            Assert.Equal("MyNetwork", this.builder.Name);
        }

        [Fact]
        public void SetRootFolderName_SetsRootFolderNameOnBuilder()
        {
            this.builder.SetRootFolderName("MyRootFolder");

            Assert.Equal("MyRootFolder", this.builder.RootFolderName);
        }

        [Fact]
        public void SetDefaultConfigFilename_SetsDefaultConfigFilenameOnBuilder()
        {
            this.builder.SetDefaultConfigFilename("MyDefaultConfigFileName");

            Assert.Equal("MyDefaultConfigFileName", this.builder.DefaultConfigFilename);
        }

        [Fact]
        public void SetTxFees_MinTxFee_SetsTxFeesOnBuilder()
        {
            this.builder.SetTxFees(1500, 0, 0);

            Assert.Equal(1500, this.builder.MinTxFee);
            Assert.Equal(0, this.builder.FallbackFee);
            Assert.Equal(0, this.builder.MinRelayTxFee);
        }

        [Fact]
        public void _SetTxFees_FallbackFee_SetsTxFeesOnBuilder()
        {
            this.builder.SetTxFees(0, 2130, 0);

            Assert.Equal(0, this.builder.MinTxFee);
            Assert.Equal(2130, this.builder.FallbackFee);
            Assert.Equal(0, this.builder.MinRelayTxFee);
        }

        [Fact]
        public void SetTxFees_MinRelayTxFee_SetsTxFeesOnBuilder()
        {
            this.builder.SetTxFees(0, 0, 1231);

            Assert.Equal(0, this.builder.MinTxFee);
            Assert.Equal(0, this.builder.FallbackFee);
            Assert.Equal(1231, this.builder.MinRelayTxFee);
        }

        [Fact]
        public void SetMaxTimeOffsetSeconds_SetsMaxTimeOffsetSecondsOnBuilder()
        {
            this.builder.SetMaxTimeOffsetSeconds(918);

            Assert.Equal(918, this.builder.MaxTimeOffsetSeconds);
        }

        [Fact]
        public void SetSetMaxTipAge_SetsMaxTipAgeOnBuilder()
        {
            this.builder.SetMaxTipAge(712);

            Assert.Equal(712, this.builder.MaxTipAge);
        }

        [Fact]
        public void AddAlias_AddsAliasToBuilder()
        {
            this.builder.AddAlias("NetworkNameByAlias");

            Assert.NotEmpty(this.builder.Aliases);
            Assert.Equal("NetworkNameByAlias", this.builder.Aliases.ElementAt(0));
        }

        [Fact]
        public void SetRPCPort_SetsRPCPortOnBuilder()
        {
            this.builder.SetRPCPort(8339);

            Assert.Equal(8339, this.builder.RPCPort);
        }

        [Fact]
        public void SetPort_SetsPortOnBuilder()
        {
            this.builder.SetPort(14000);

            Assert.Equal(14000, this.builder.Port);
        }

        [Fact]
        public void SetMagic_SetsMagicOnBuilder()
        {
            this.builder.SetMagic(1231241);

            Assert.Equal((uint)1231241, this.builder.Magic);
        }

        [Fact]
        public void AddDNSSeeds_SetsDNSSeedsOnBuilder()
        {
            this.builder.AddDNSSeeds(new DNSSeedData[] { new DNSSeedData("dnsNode1", "3.4.2.1") });

            Assert.NotEmpty(this.builder.Seeds);
            Assert.Equal("3.4.2.1", this.builder.Seeds.ToList()[0].Host);
            Assert.Equal("dnsNode1", this.builder.Seeds.ToList()[0].Name);
        }

        [Fact]
        public void AddSeeds_SetsSeedsOnBuilder()
        {
            this.builder.AddSeeds(new NetworkAddress[] { new NetworkAddress(IPAddress.Parse("::ffff:0:0"), 1234) });

            Assert.NotEmpty(this.builder.FixedSeeds);
            Assert.Equal(IPAddress.Parse("::ffff:0:0"), this.builder.FixedSeeds.ToList()[0].Endpoint.Address);
            Assert.Equal(1234, this.builder.FixedSeeds.ToList()[0].Endpoint.Port);
        }

        [Fact]
        public void SetConsensus_SetsConsensusOnNetwork()
        {
            var consensus = new Consensus() { CoinType = 15 };

            this.builder.SetConsensus(consensus);

            Assert.Equal(15, this.builder.Consensus.CoinType);
        }

        [Fact]
        public void SetGenesis_SetsGenesisOnBuilder()
        {
            var genesis = new Block(new BlockHeader() { Version = 10 });
            this.builder.SetGenesis(genesis);

            Assert.Equal(10, this.builder.Genesis.Header.Version);
        }

        [Fact]
        public void SetBase58Bytes_SetsBase58PrefixesOnBuilder()
        {
            this.builder.SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) });

            Assert.Equal(new byte[] { (196) }, this.builder.Base58Prefixes[Base58Type.SCRIPT_ADDRESS]);
        }

        [Fact]
        public void SetSetBech32_HumanReadable_SetsBech32EncodersOnBuilder()
        {
            this.builder.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, "bc");

            Assert.Equal(Encoders.Bech32("bc").HumanReadablePart, this.builder.Bech32Prefixes[Bech32Type.WITNESS_PUBKEY_ADDRESS].HumanReadablePart);
        }

        [Fact]
        public void SetSetBech32_ExistingEncoder_SetsBech32EncodersOnBuilder()
        {
            Bech32Encoder encoder = Encoders.Bech32("bc");
            this.builder.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, encoder);

            Assert.Equal(encoder.HumanReadablePart, this.builder.Bech32Prefixes[Bech32Type.WITNESS_PUBKEY_ADDRESS].HumanReadablePart);
        }

        [Fact]
        public void SetCheckpoints_SetsCheckpointsOnBuilder()
        {
            var checkpoints = new Dictionary<int, CheckpointInfo>()
            {
                { 33333, new CheckpointInfo(new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6")) },
            };
            this.builder.SetCheckpoints(checkpoints);

            Assert.NotEmpty(this.builder.Checkpoints);
            KeyValuePair<int, CheckpointInfo> resultingCheckpoint = this.builder.Checkpoints.ElementAt(0);
            Assert.Equal(33333, resultingCheckpoint.Key);
            Assert.Equal(new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6"), resultingCheckpoint.Value.Hash);
            Assert.Null(resultingCheckpoint.Value.StakeModifierV2);
        }

        [Fact]
        public void BuildAndRegister_SetNoName_ThrowsInvalidOperationException()
        {
            this.builder.SetName(null);

            Assert.Throws<InvalidOperationException>(() => this.builder.BuildAndRegister());
        }

        [Fact]
        public void BuildAndRegister_RegisterTwice_ThrowsInvalidOperationException()
        {
            this.builder.SetName("MyDuplicateNetwork");
            this.builder.SetGenesis(new Block(new BlockHeader() { Version = 10 }));
            this.builder.SetConsensus(new Consensus() { CoinType = 15 });

            this.builder.BuildAndRegister();

            Assert.Throws<InvalidOperationException>(() => this.builder.BuildAndRegister());
        }

        [Fact]
        public void BuildAndRegister_ConsensusNotProvided_ThrowsInvalidOperationException()
        {
            var genesis = new Block(new BlockHeader() { Version = 10 });

            this.builder.SetName("ConsensusNotSetNetwork");
            this.builder.SetGenesis(genesis);
            this.builder.SetConsensus(null);

            Assert.Throws<InvalidOperationException>(() => this.builder.BuildAndRegister());
        }

        [Fact]
        public void BuildAndRegister_GenesisNotProvided_ThrowsInvalidOperationException()
        {
            var consensus = new Consensus() { CoinType = 15 };
            this.builder.SetName("GenesisNotSetNetwork");
            this.builder.SetGenesis(null);
            this.builder.SetConsensus(consensus);

            Assert.Throws<InvalidOperationException>(() => this.builder.BuildAndRegister());
        }

        [Fact]
        public void BuildAndRegister_SetName_SetsNetworkNameOnNetwork()
        {
            this.PrepareValidNetwork("MyNetwork");

            Network result = this.builder.BuildAndRegister();

            Assert.Equal("MyNetwork", result.Name);
        }

        [Fact]
        public void BuildAndRegister_SetRootFolderName_SetsRootFolderNameOnNetwork()
        {
            this.PrepareValidNetwork("RootFolderNetwork");
            this.builder.SetRootFolderName("MyRootFolder");

            Network result = this.builder.BuildAndRegister();

            Assert.Equal("MyRootFolder", result.RootFolderName);
        }

        [Fact]
        public void BuildAndRegister_SetDefaultConfigFilename_SetsDefaultConfigFilenameOnNetwork()
        {
            this.PrepareValidNetwork("DefaultConfigFileNetwork");
            this.builder.SetDefaultConfigFilename("MyDefaultConfigFileName");

            Network result = this.builder.BuildAndRegister();

            Assert.Equal("MyDefaultConfigFileName", result.DefaultConfigFilename);
        }

        [Fact]
        public void BuildAndRegister_SetTxFees_MinTxFee_SetsTxFeesOnNetwork()
        {
            this.PrepareValidNetwork("MinTxFeeNetwork");
            this.builder.SetTxFees(1500, 0, 0);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(1500, result.MinTxFee);
            Assert.Equal(0, result.FallbackFee);
            Assert.Equal(0, result.MinRelayTxFee);
        }

        [Fact]
        public void BuildAndRegister_SetTxFees_FallbackFee_SetsTxFeesOnNetwork()
        {
            this.PrepareValidNetwork("FallbackFeeNetwork");
            this.builder.SetTxFees(0, 2130, 0);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(0, result.MinTxFee);
            Assert.Equal(2130, result.FallbackFee);
            Assert.Equal(0, result.MinRelayTxFee);
        }

        [Fact]
        public void BuildAndRegister_SetTxFees_MinRelayTxFee_SetsTxFeesOnNetwork()
        {
            this.PrepareValidNetwork("MinRelayTxFeeNetwork");
            this.builder.SetTxFees(0, 0, 1231);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(0, result.MinTxFee);
            Assert.Equal(0, result.FallbackFee);
            Assert.Equal(1231, result.MinRelayTxFee);
        }

        [Fact]
        public void BuildAndRegister_SetMaxTimeOffsetSeconds_SetsMaxTimeOffsetSecondsOnNetwork()
        {
            this.PrepareValidNetwork("MaxTimeOffsetSecondsNetwork");
            this.builder.SetMaxTimeOffsetSeconds(918);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(918, result.MaxTimeOffsetSeconds);
        }

        [Fact]
        public void BuildAndRegister_SetSetMaxTipAge_SetsMaxTipAgeOnNetwork()
        {
            this.PrepareValidNetwork("SetMaxTipAgeNetwork");
            this.builder.SetMaxTipAge(712);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(712, result.MaxTipAge);
        }

        [Fact]
        public void BuildAndRegister_AddAlias_RegistersNetworkWithAliasToNetworks()
        {
            this.PrepareValidNetwork("AliasNetwork");
            this.builder.AddAlias("NetworkNameByAlias");

            Network result = this.builder.BuildAndRegister();

            Assert.Equal("AliasNetwork", result.Name);
            var network = Network.GetNetwork("NetworkNameByAlias");
            Assert.Equal(network.Name, result.Name);
        }

        [Fact]
        public void BuildAndRegister_SetRPCPort_SetsRPCPortOnNetwork()
        {
            this.PrepareValidNetwork("SetRPCPortNetwork");
            this.builder.SetRPCPort(8339);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(8339, result.RPCPort);
        }

        [Fact]
        public void BuildAndRegister_SetPort_SetsPortOnNetwork()
        {
            this.PrepareValidNetwork("SetPortNetwork");
            this.builder.SetPort(14000);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(14000, result.DefaultPort);
        }

        [Fact]
        public void BuildAndRegister_SetMagic_SetsMagicOnNetwork()
        {
            this.PrepareValidNetwork("SetMagicNetwork");
            this.builder.SetMagic(1231241);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal((uint)1231241, result.Magic);
        }

        [Fact]
        public void BuildAndRegister_AddDNSSeeds_SetsDNSSeedsOnNetwork()
        {
            this.PrepareValidNetwork("SetDNSSeedsNetwork");
            this.builder.AddDNSSeeds(new DNSSeedData[] { new DNSSeedData("dnsNode1", "3.4.2.1") });

            Network result = this.builder.BuildAndRegister();

            Assert.NotEmpty(result.DNSSeeds);
            Assert.Equal("3.4.2.1", result.DNSSeeds.ToList()[0].Host);
            Assert.Equal("dnsNode1", result.DNSSeeds.ToList()[0].Name);
        }

        [Fact]
        public void BuildAndRegister_AddSeeds_SetsSeedsOnNetwork()
        {
            this.PrepareValidNetwork("SetSeedsNetwork");
            this.builder.AddSeeds(new NetworkAddress[] { new NetworkAddress(IPAddress.Parse("::ffff:0:0"), 1234) });

            Network result = this.builder.BuildAndRegister();

            Assert.NotEmpty(result.SeedNodes);
            Assert.Equal(IPAddress.Parse("::ffff:0:0"), result.SeedNodes.ToList()[0].Endpoint.Address);
            Assert.Equal(1234, result.SeedNodes.ToList()[0].Endpoint.Port);
        }

        [Fact]
        public void BuildAndRegister_SetConsensus_SetGenesis_SetsConsensusAndGenesisOnNetwork()
        {
            var genesis = new Block(new BlockHeader() { Version = 10 });
            var consensus = new Consensus() { CoinType = 15 };

            this.builder.SetName("SetConsensusNetwork");
            this.builder.SetConsensus(consensus);
            this.builder.SetGenesis(genesis);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(15, result.Consensus.CoinType);
            Assert.Equal(10, result.GetGenesis().Header.Version);
        }

        [Fact]
        public void BuildAndRegister_SetBase58Bytes_SetsBase58PrefixesOnNetwork()
        {
            this.PrepareValidNetwork("SetBase58BytesNetwork");
            this.builder.SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) });

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(new byte[] { (196) }, result.base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
        }

        [Fact]
        public void BuildAndRegister_SetSetBech32_HumanReadable_SetsBech32EncodersOnNetwork()
        {
            this.PrepareValidNetwork("SetBech34ReadableNetwork");
            this.builder.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, "bc");

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(Encoders.Bech32("bc").HumanReadablePart, result.bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].HumanReadablePart);
        }

        [Fact]
        public void BuildAndRegister_SetSetBech32_ExistingEncoder_SetsBech32EncodersOnNetwork()
        {
            Bech32Encoder encoder = Encoders.Bech32("bc");
            this.PrepareValidNetwork("SetBech32EncoderNetwork");
            this.builder.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, encoder);

            Network result = this.builder.BuildAndRegister();

            Assert.Equal(encoder.HumanReadablePart, result.bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].HumanReadablePart);
        }

        [Fact]
        public void BuildAndRegister_SetCheckpoints_SetsCheckpointsOnNetwork()
        {
            var checkpoints = new Dictionary<int, CheckpointInfo>()
            {
                { 33333, new CheckpointInfo(new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6")) },
            };
            this.PrepareValidNetwork("SetCheckpointsNetwork");
            this.builder.SetCheckpoints(checkpoints);

            Network result = this.builder.BuildAndRegister();

            Assert.NotEmpty(result.Checkpoints);
            KeyValuePair<int, CheckpointInfo> resultingCheckpoint = result.Checkpoints.ElementAt(0);
            Assert.Equal(33333, resultingCheckpoint.Key);
            Assert.Equal(new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6"), resultingCheckpoint.Value.Hash);
            Assert.Null(resultingCheckpoint.Value.StakeModifierV2);
        }

        private void PrepareValidNetwork(string networkName)
        {
            this.builder.SetName(networkName);
            this.builder.SetGenesis(new Block(new BlockHeader() { Version = 10 }));
            this.builder.SetConsensus(new Consensus() { CoinType = 15 });
        }
    }
}
