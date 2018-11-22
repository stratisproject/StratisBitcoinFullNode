using System;
using NBitcoin;

using Stratis.Bitcoin.Features.PoA;

using Xunit;
using Xunit.Abstractions;

namespace FedKeyPairGen
{
    public class GenesisMiner
    {
        private readonly ITestOutputHelper output;

        public GenesisMiner(ITestOutputHelper output)
        {
            this.output = output;
        }

        //[Fact]
        [Fact(Skip = "This is not a test, it is meant to be run upon creating a network")]
        public void Run_MineGenesis()
        {
            var consensusFactory = new PoAConsensusFactory();
            string coinbaseText = "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/";
            this.MineGenesisBlocks(consensusFactory, coinbaseText);
        }

        public void MineGenesisBlocks(ConsensusFactory consensusFactory, string coinbaseText)
        {
            this.output.WriteLine("Looking for genesis blocks  for the 3 networks, this might take a while.");

            Block genesisMain = Network.MineGenesisBlock(consensusFactory, coinbaseText, new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), Money.Coins(50m));
            BlockHeader headerMain = genesisMain.Header;

            this.output.WriteLine("-- MainNet network --");
            this.output.WriteLine("bits: " + headerMain.Bits);
            this.output.WriteLine("nonce: " + headerMain.Nonce);
            this.output.WriteLine("time: " + headerMain.Time);
            this.output.WriteLine("version: " + headerMain.Version);
            this.output.WriteLine("hash: " + headerMain.GetHash());
            this.output.WriteLine("merkleroot: " + headerMain.HashMerkleRoot);

            Block genesisTest = Network.MineGenesisBlock(consensusFactory, coinbaseText, new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000")), Money.Coins(50m));
            BlockHeader headerTest = genesisTest.Header;
            this.output.WriteLine("-- TestNet network --");
            this.output.WriteLine("bits: " + headerTest.Bits);
            this.output.WriteLine("nonce: " + headerTest.Nonce);
            this.output.WriteLine("time: " + headerTest.Time);
            this.output.WriteLine("version: " + headerTest.Version);
            this.output.WriteLine("hash: " + headerTest.GetHash());
            this.output.WriteLine("merkleroot: " + headerTest.HashMerkleRoot);

            Block genesisReg = Network.MineGenesisBlock(consensusFactory, coinbaseText, new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), Money.Coins(50m));
            BlockHeader headerReg = genesisReg.Header;
            this.output.WriteLine("-- RegTest network --");
            this.output.WriteLine("bits: " + headerReg.Bits);
            this.output.WriteLine("nonce: " + headerReg.Nonce);
            this.output.WriteLine("time: " + headerReg.Time);
            this.output.WriteLine("version: " + headerReg.Version);
            this.output.WriteLine("hash: " + headerReg.GetHash());
            this.output.WriteLine("merkleroot: " + headerReg.HashMerkleRoot);
        }
    }
}