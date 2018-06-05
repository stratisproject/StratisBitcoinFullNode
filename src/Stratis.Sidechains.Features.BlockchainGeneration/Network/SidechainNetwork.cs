using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    public class SidechainNetwork
    {
        public const string SidechainMainName = "SidechainMain";
        public const string SidechainTestName = "SidechainTest";
        public const string SidechainRegTestName = "SidechainRegTest";

        public static Network SidechainMain => Network.GetNetwork(SidechainMainName) ?? InitSidechainMain();        

        public static Network SidechainTest => Network.GetNetwork(SidechainTestName) ?? InitSidechainTest();

        public static Network SidechainRegTest => Network.GetNetwork(SidechainRegTestName) ?? InitSidechainRegTest();

        private static Network InitSidechainMain()
        {
            SidechainInfo sidechainInfo = SidechainIdentifier.Instance.InfoProvider
               .GetSidechainInfo(SidechainIdentifier.Instance.Name);
            var networkInfo = sidechainInfo.MainNet;

            Block.BlockSignature = true;
            Transaction.TimeStamp = true;

            var consensus = new Consensus();

            consensus.NetworkOptions = new NetworkOptions() { IsProofOfStake = true };
            consensus.GetPoWHash = (n, h) => NBitcoin.Crypto.HashX13.Instance.Hash(h.ToBytes(options: n));

            consensus.SubsidyHalvingInterval = 210000;
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.PowLimit = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.PowAllowMinDifficultyBlocks = false;
            consensus.PowNoRetargeting = false;
            consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            
            consensus.LastPOWBlock = 12500;

            consensus.ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));

            consensus.CoinType =  sidechainInfo.CoinType;

            consensus.DefaultAssumeValid = null;//new uint256("0x8c2cf95f9ca72e13c8c4cdf15c2d7cc49993946fb49be4be147e106d502f1869"); // 642930

            Block genesis = CreateSidechainGenesisBlock(networkInfo.Time, networkInfo.Nonce, 0x1e0fffff, 1, Money.Zero);
            consensus.HashGenesisBlock = genesis.GetHash(consensus.NetworkOptions);

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x70;
            messageStart[1] = 0x35;
            messageStart[2] = 0x22;
            messageStart[3] = 0x05;
            var magic = BitConverter.ToUInt32(messageStart, 0); //0x5223570; 

            Assert(consensus.HashGenesisBlock.ToString() == networkInfo.GenesisHashHex);

            var builder = new NetworkBuilder()
                .SetName(SidechainNetwork.SidechainMainName)
                .SetRootFolderName(SidechainIdentifier.Instance.Name)
                .SetDefaultConfigFilename($"{SidechainIdentifier.Instance.Name}.conf")
                .SetConsensus(consensus)
                .SetMagic(magic)
                .SetGenesis(genesis)
                .SetPort(networkInfo.Port)
                .SetRPCPort(networkInfo.RpcPort)
                .SetTxFees(10000, 60000, 10000)
                .SetMaxTipAge(Network.StratisDefaultMaxTipAgeInSeconds)
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { ((byte)networkInfo.AddressPrefix) })
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (125) })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (63 + 128) })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) })
                .SetBase58Bytes(Base58Type.PASSPHRASE_CODE, new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 })
                .SetBase58Bytes(Base58Type.CONFIRMATION_CODE, new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A })
                .SetBase58Bytes(Base58Type.STEALTH_ADDRESS, new byte[] { 0x2a })
                .SetBase58Bytes(Base58Type.ASSET_ID, new byte[] { 23 })
                .SetBase58Bytes(Base58Type.COLORED_ADDRESS, new byte[] { 0x13 })
                .SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, "bc")
                .SetBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, "bc");


            return builder.BuildAndRegister();
        }

        private static Network InitSidechainTest()
        {
            SidechainInfo sidechainInfo = SidechainIdentifier.Instance.InfoProvider
               .GetSidechainInfo(SidechainIdentifier.Instance.Name);
            var networkInfo = sidechainInfo.TestNet;

            Block.BlockSignature = true;
            Transaction.TimeStamp = true;

            var consensus = SidechainNetwork.SidechainMain.Consensus.Clone();
            consensus.PowLimit = new Target(uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000"));

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x71;
            messageStart[1] = 0x31;
            messageStart[2] = 0x21;
            messageStart[3] = 0x11;
            var magic = BitConverter.ToUInt32(messageStart, 0); //0x5223570; 

            var genesis = Network.StratisMain.GetGenesis().Clone();
            genesis.Header.Time = networkInfo.Time;              //1510160966;   //updated for sidechains
            genesis.Header.Nonce = networkInfo.Nonce;            // 2433759;     //updated for sidechains
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash();

            Assert(consensus.HashGenesisBlock.ToString() == networkInfo.GenesisHashHex);

            consensus.DefaultAssumeValid = null; // turn off assumevalid for sidechains.

            var builder = new NetworkBuilder()
                .SetName(SidechainTestName)
                .SetRootFolderName(SidechainIdentifier.Instance.Name)
                .SetDefaultConfigFilename($"{SidechainIdentifier.Instance.Name}.conf")
                .SetConsensus(consensus)
                .SetMagic(magic)
                .SetGenesis(genesis)
                .SetPort(networkInfo.Port)              //36178 updated for sidechains
                .SetRPCPort(networkInfo.RpcPort)        //36174 updated for sidechains
                .SetTxFees(10000, 60000, 10000)
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { ((byte)networkInfo.AddressPrefix) })              //65     //updated for sidechains
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) });

            return builder.BuildAndRegister();
        }

        private static Network InitSidechainRegTest()
        {
            // TODO: move this to Networks
            var net = Network.GetNetwork(SidechainNetwork.SidechainRegTestName);
            if (net != null)
                return net;

            var networkInfo = SidechainIdentifier.Instance.InfoProvider
                .GetSidechainInfo(SidechainIdentifier.Instance.Name).RegTest;

            Block.BlockSignature = true;
            Transaction.TimeStamp = true;

            var consensus = SidechainNetwork.SidechainTest.Consensus.Clone();
            consensus.PowLimit = new Target(uint256.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;

            var messageStart = new byte[4];
            messageStart[0] = 0xcd;
            messageStart[1] = 0xf2;
            messageStart[2] = 0xc0;
            messageStart[3] = 0xef;
            var magic = BitConverter.ToUInt32(messageStart, 0);

            var genesis = Network.StratisMain.GetGenesis();
            genesis.Header.Time = networkInfo.Time;
            genesis.Header.Nonce = networkInfo.Nonce;
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash(consensus.NetworkOptions);

            Assert(consensus.HashGenesisBlock.ToString() == networkInfo.GenesisHashHex);

            consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.

            var builder = new NetworkBuilder()
                .SetName(SidechainNetwork.SidechainRegTestName)
                .SetRootFolderName(SidechainIdentifier.Instance.Name)
                .SetDefaultConfigFilename($"{SidechainIdentifier.Instance.Name}.conf")
                .SetConsensus(consensus)
                .SetMagic(magic)
                .SetGenesis(genesis)
                .SetPort(networkInfo.Port)
                .SetRPCPort(networkInfo.RpcPort)
                .SetMaxTipAge(Network.StratisDefaultMaxTipAgeInSeconds)
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { ((byte)networkInfo.AddressPrefix) })
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) });

            return builder.BuildAndRegister();
        }

        internal static Block CreateSidechainGenesisBlock(uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            string pszTimestamp = "http://www.theonion.com/article/olympics-head-priestess-slits-throat-official-rio--53466";
            return CreateSidechainGenesisBlock(pszTimestamp, nTime, nNonce, nBits, nVersion, genesisReward);
        }

        private static Block CreateSidechainGenesisBlock(string pszTimestamp, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            Transaction txNew = new Transaction();
            txNew.Version = 1;
            txNew.Time = nTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(0), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)42 }
                }, Op.GetPushOp(NBitcoin.DataEncoders.Encoders.ASCII.DecodeData(pszTimestamp)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = genesisReward,
            });
            Block genesis = new Block();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
            genesis.Header.Bits = nBits;
            genesis.Header.Nonce = nNonce;
            genesis.Header.Version = nVersion;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();
            return genesis;
        }

        private static void Assert(bool condition)
        {
            // TODO: use Guard when this moves to the FN.
            if (!condition)
            {
                throw new InvalidOperationException("Invalid network");
            }
        }
    }
}
