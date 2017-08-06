using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Obsidian.Coin
{
    static class GenesisBlock
    {
	    public static Block CreateMainGenesisBlock()
	    {
			uint nTime = 1470467000; // 08/05/2017 @ 12:00am (UTC) = 1501891200
			uint nNonce = 1831645;// 1 // we don't need a hash with leading zeros, so this is arbitrary
		    uint nBits = 0x1e0fffff; // https://bitcoin.org/en/developer-reference#target-nbits

			return  CreateGenesisBlock(nTime, nNonce, nBits, 1, Money.Zero);
		}

	    static Block CreateGenesisBlock(uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
	    {
		    string pszTimestamp = "http://www.theonion.com/article/olympics-head-priestess-slits-throat-official-rio--53466";
			// string pszTimestamp = "https://en.wikipedia.org/wiki/Brave_New_World"; // no special time and yes, we premined!

		    Transaction txNew = new Transaction();
		    txNew.Version = 1;
		    txNew.Time = nTime;
		    txNew.AddInput(new TxIn()
		    {
			    ScriptSig = new Script(Op.GetPushOp(0), new Op
			    {
				    Code = (OpcodeType)0x1,
				    PushData = new[] { (byte)42 }
			    }, Op.GetPushOp(Encoders.ASCII.DecodeData(pszTimestamp)))
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
	}
}
