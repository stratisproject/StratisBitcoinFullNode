//using System;
//using System.Collections.Generic;
//using System.Text;
//using NBitcoin;
//using Xunit;

//namespace City.Chain.Tests
//{
//    public class BlockchainStartup
//    {
//        [Fact]
//        public void GenerateGenesisBlocks()
//        {
//            List<string> contents = new List<string>();

//            // CITY GENESIS BLOCK:
//            // 2018-07-27: "Bitcoin’s roots are in anarcho-capitalism, a movement that aspires to reproduce the mechanisms of the free market without the need for banks or state bodies to enforce rules."
//            // URL: https://www.newscientist.com/article/mg23831841-200-how-to-think-about-the-blockchain/
//            var urlMain = "July 27, 2018, New Scientiest, Bitcoin’s roots are in anarcho-capitalism";

//            // CITY TEST BLOCK:
//            // 2017-08-18: "Libertarianisme er en politisk tankeretning som legger stor vekt på individuell frihet, og prosjektet er blitt omtalt over hele verden på sentrale nettsider for folk som handler med kryptovalutaer, altså penger og sikre verdipapirer som fungerer helt uten en sentral myndighet."
//            // URL: https://morgenbladet.no/aktuelt/2017/08/privatlivets-fred-i-liberstad
//            var urlTest = "August 18 2017, Morgenbladet, Money that work without a central authority";

//            // CITY REG-TEST BLOCK:
//            // 2018-07-26: "We don’t need to fight the existing system, we just need to create a new one."
//            // URL: https://futurethinkers.org/vit-jedlicka-liberland/
//            var urlRegTest = "July 26, 2018, Future Thinkers, We don’t need to fight existing system, we create a new one";

//            uint testTime = 1538395200;
//            var testBlock = Network.MineGenesisBlock(new PosConsensusFactory(), urlTest, new Target(new uint256("000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), Money.Zero, testTime);

//            contents.Add("TEST GENESIS BLOCK:");
//            contents.Add("Test Nonce: " + testBlock.Header.Nonce);
//            contents.Add("Test Time: " + testBlock.Header.Time);
//            contents.Add("Test Bits: " + testBlock.Header.Bits.ToCompact().ToString("X2"));
//            contents.Add("Test Hash: " + testBlock.Header.ToString());
//            contents.Add("Test Hash Merkle Root: " + testBlock.Header.HashMerkleRoot);

//            uint regTestTime = 1538568000;
//            var regTestBlock = Network.MineGenesisBlock(new PosConsensusFactory(), urlRegTest, new Target(new uint256("0000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), Money.Zero, regTestTime);

//            contents.Add("");
//            contents.Add("TEST REG GENESIS BLOCK:");
//            contents.Add("Reg Test Nonce: " + regTestBlock.Header.Nonce);
//            contents.Add("Reg Test Time: " + regTestBlock.Header.Time);
//            contents.Add("Reg Test Bits: " + regTestBlock.Header.Bits.ToCompact().ToString("X2"));
//            contents.Add("Reg Test Hash: " + regTestBlock.Header.ToString());
//            contents.Add("Reg Test Hash Merkle Root: " + regTestBlock.Header.HashMerkleRoot);

//            uint mainTime = 1538481600;
//            var mainBlock = Network.MineGenesisBlock(new PosConsensusFactory(), urlMain, new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), Money.Zero, mainTime);

//            contents.Add("");
//            contents.Add("MAIN GENESIS BLOCK:");
//            contents.Add("Main Nonce: " + mainBlock.Header.Nonce);
//            contents.Add("Main Time: " + mainBlock.Header.Time);
//            contents.Add("Main Bits: " + mainBlock.Header.Bits.ToCompact().ToString("X2"));
//            contents.Add("Main Hash: " + mainBlock.Header.ToString());
//            contents.Add("Main Hash Merkle Root: " + mainBlock.Header.HashMerkleRoot);

//            System.IO.File.AppendAllLines("genesis-output.txt", contents);
//        }
//    }
//}
