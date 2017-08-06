using System.Diagnostics;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Obsidian.Coin
{
    static class GenesisBlock
    {
	    public static Block CreateMainGenesisBlock()
	    {
			uint nTime = 1501891200; // 08/05/2017 @ 12:00am (UTC) = 1501891200
			uint nNonce = 1; // why 1 - we don't need a hash with leading zeros, so this is arbitrary
		    uint nBits = 0x1e0fffff; // https://bitcoin.org/en/developer-reference#target-nbits

			var mainGenesisBlock = CreateGenesisBlock(nTime, nNonce, nBits, 1, Money.Zero);

			Debug.WriteLine(mainGenesisBlock.GetHash());
		    Debug.Assert(mainGenesisBlock.GetHash() == uint256.Parse("0x2406a70d16f7d5b8b45f3ec41a0b00efeef10acce8c7ab71d614ad89845be03c"));

			Debug.WriteLine(mainGenesisBlock.Header.HashMerkleRoot);
		    Debug.Assert(mainGenesisBlock.Header.HashMerkleRoot == uint256.Parse("0x47037e5436a8125675e227c1fe090f6f55bd130f9d47a2a4c94f0c1830b9d9f1"));
		    return mainGenesisBlock;
	    }

	    static Block CreateGenesisBlock(uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
	    {
			string pszTimestamp = "https://en.wikipedia.org/w/index.php?title=Brave_New_World&oldid=792463924";

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
