﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Obsidian.Coin
{
    public static class StratisNetworks
    {
	    public static Network InitStratisTest()
	    {
		    Block.BlockSignature = true;
		    Transaction.TimeStamp = true;

		    var consensus = Network.StratisMain.Consensus.Clone();
		    consensus.PowLimit = new Target(uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000"));

		    // The message start string is designed to be unlikely to occur in normal data.
		    // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
		    // a large 4-byte int at any alignment.
		    var pchMessageStart = new byte[4];
		    pchMessageStart[0] = 0x71;
		    pchMessageStart[1] = 0x31;
		    pchMessageStart[2] = 0x21;
		    pchMessageStart[3] = 0x11;
		    var magic = BitConverter.ToUInt32(pchMessageStart, 0); //0x5223570; 

		    var genesis = Network.StratisMain.GetGenesis().Clone();
		    genesis.Header.Time = 1493909211;
		    genesis.Header.Nonce = 2433759;
		    genesis.Header.Bits = consensus.PowLimit;
		    consensus.HashGenesisBlock = genesis.GetHash();

		    //assert(consensus.HashGenesisBlock == uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"));

		    var builder = new NetworkBuilder()
			    .SetName("StratisTest")
			    .SetConsensus(consensus)
			    .SetMagic(magic)
			    .SetGenesis(genesis)
			    .SetPort(26178)
			    .SetRPCPort(26174)
			    .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (65) })
			    .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
			    .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
			    .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
			    .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
			    .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
			    .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) })

#if !NOSOCKET
			    .AddDNSSeeds(new DNSSeedData[]
			    {
				    //new DNSSeedData("stratisplatform.com", "testnode1.stratisplatform.com"),
			    });
#endif

		    return builder.BuildAndRegister();
	    }
	}
}
