using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA;
using Xunit;
using Xunit.Abstractions;

namespace FederationSetup
{
    public class GenesisMiner
    {
        private readonly ITestOutputHelper output;

        public GenesisMiner(ITestOutputHelper output = null)
        {
            if (output == null) return;
            this.output = output;
        }

        //[Fact]
        [Fact(Skip = "This is not a test, it is meant to be run upon creating a network")]
        public void Run_MineGenesis()
        {
            var consensusFactory = new PoAConsensusFactory();
            string coinbaseText = "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/";
            this.output.WriteLine(this.MineGenesisBlocks(consensusFactory, coinbaseText));
        }

        public string MineGenesisBlocks(ConsensusFactory consensusFactory, string coinbaseText)
        {
            var output = new StringBuilder();

            Console.WriteLine("Looking for genesis blocks for the 3 networks, this might take a while.");
            Console.WriteLine(Environment.NewLine);

            var targets = new Dictionary<uint256, string>
            {
                { new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"), "-- MainNet network --" },
                { new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"), "-- TestNet network --" },
                { new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"), "-- RegTest network --" }
            };

            BlockHeader header = null;

            foreach (KeyValuePair<uint256, string> target in targets)
            {
                header = this.GeneterateBlock(consensusFactory, coinbaseText, target.Key);
                output.AppendLine(this.NetworkOutput(header, target.Value));
            }

            return output.ToString();
        }

        private BlockHeader GeneterateBlock(ConsensusFactory consensusFactory, string coinbaseText, uint256 target)
        {
            Block genesis = Network.MineGenesisBlock(consensusFactory, coinbaseText, new Target(target), Money.Coins(50m));

            return genesis.Header;
        }

        private string NetworkOutput(BlockHeader header, string network)
        {
            var output = new StringBuilder();

            output.AppendLine(network);
            output.AppendLine("bits: " + header.Bits);
            output.AppendLine("nonce: " + header.Nonce);
            output.AppendLine("time: " + header.Time);
            output.AppendLine("version: " + header.Version);
            output.AppendLine("hash: " + header.GetHash());
            output.AppendLine("merkleroot: " + header.HashMerkleRoot);
            output.AppendLine(Environment.NewLine);

            return output.ToString();
        }
    }
}