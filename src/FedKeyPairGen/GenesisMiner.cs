using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
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
            var consensusFactory = new SmartContractPoAConsensusFactory();
            string coinbaseText = "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/";
            this.output.WriteLine(this.MineGenesisBlocks(consensusFactory, coinbaseText));
        }

        public string MineGenesisBlocks(SmartContractPoAConsensusFactory consensusFactory, string coinbaseText)
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

            foreach (KeyValuePair<uint256, string> target in targets)
            {
                Block genesisBlock = this.GeneterateBlock(consensusFactory, coinbaseText, target.Key);
                output.AppendLine(this.NetworkOutput(genesisBlock, target.Value, coinbaseText));
            }

            return output.ToString();
        }

        private Block GeneterateBlock(SmartContractPoAConsensusFactory consensusFactory, string coinbaseText, uint256 target)
        {
            return MineGenesisBlock(consensusFactory, coinbaseText, new Target(target), Money.Zero);
        }

        private string NetworkOutput(Block genesisBlock, string network, string coinbaseText)
        {
            SmartContractPoABlockHeader header = (SmartContractPoABlockHeader) genesisBlock.Header;

            var output = new StringBuilder();
            output.AppendLine(network);
            output.AppendLine("bits: " + header.Bits);
            output.AppendLine("nonce: " + header.Nonce);
            output.AppendLine("time: " + header.Time);
            output.AppendLine("version: " + header.Version);
            output.AppendLine("hash: " + genesisBlock.GetHash());
            output.AppendLine("merkleroot: " + header.HashMerkleRoot);
            output.AppendLine("coinbase text: " + coinbaseText);
            output.AppendLine("hash state root: " + header.HashStateRoot);
            output.AppendLine(Environment.NewLine);

            return output.ToString();
        }

        public static Block MineGenesisBlock(SmartContractPoAConsensusFactory consensusFactory, string coinbaseText, Target target, Money genesisReward, int version = 1)
        {
            if (consensusFactory == null)
                throw new ArgumentException($"Parameter '{nameof(consensusFactory)}' cannot be null. Use 'new ConsensusFactory()' for Bitcoin-like proof-of-work blockchains and 'new PosConsensusFactory()' for Stratis-like proof-of-stake blockchains.");

            if (string.IsNullOrEmpty(coinbaseText))
                throw new ArgumentException($"Parameter '{nameof(coinbaseText)}' cannot be null. Use a news headline or any other appropriate string.");

            if (target == null)
                throw new ArgumentException($"Parameter '{nameof(target)}' cannot be null. Example use: new Target(new uint256(\"0000ffff00000000000000000000000000000000000000000000000000000000\"))");

            if (coinbaseText.Length >= 92)
                throw new ArgumentException($"Parameter '{nameof(coinbaseText)}' should be shorter than 92 characters.");

            if (genesisReward == null)
                throw new ArgumentException($"Parameter '{nameof(genesisReward)}' cannot be null. Example use: 'Money.Coins(50m)'.");

            DateTimeOffset time = DateTimeOffset.Now;
            uint unixTime = Utils.DateTimeToUnixTime(time);

            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = (uint)version;
            txNew.Time = unixTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(
                    Op.GetPushOp(0),
                    new Op()
                    {
                        Code = (OpcodeType)0x1,
                        PushData = new[] { (byte)42 }
                    },
                    Op.GetPushOp(Encoders.ASCII.DecodeData(coinbaseText)))
            });

            txNew.AddOutput(new TxOut()
            {
                Value = genesisReward,
            });

            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = time;
            genesis.Header.Bits = target;
            genesis.Header.Nonce = 0;
            genesis.Header.Version = version;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();

            ((SmartContractPoABlockHeader)genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856"); // Set StateRoot to empty trie.

            // Iterate over the nonce until the proof-of-work is valid.
            // This will mean the block header hash is under the target.
            while (!genesis.CheckProofOfWork())
            {
                genesis.Header.Nonce++;
                if (genesis.Header.Nonce == 0)
                    genesis.Header.Time++;
            }

            return genesis;
        }
    }
}