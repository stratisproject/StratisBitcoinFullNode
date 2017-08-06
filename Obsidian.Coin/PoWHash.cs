using System;
using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.Crypto;

namespace Obsidian.Coin
{
	static class ObsidianPoWHash
	{
		public static uint256 GetPoWHash(BlockHeader blockheader)
		{
			byte[] headerBytes = blockheader.ToBytes();
			if (NetConfig.ObsidianNet == ObsidianNet.Main && NetConfig.UseSha512OnMain ||
				NetConfig.ObsidianNet == ObsidianNet.Test && NetConfig.UseSha512OnTest)
				return new uint256(GetSha512256(headerBytes));
			return HashX13.Instance.Hash(headerBytes);
		}

		static byte[] GetSha512256(byte[] data)
		{
			byte[] sha512256 = new byte[32];
			// the sha512 object is probably reusable, but check first before optimizing.
			using (var sha512 = SHA512.Create())
			{
				var sha512Full = sha512.ComputeHash(data);
				// it's safe to truncate SHA512, TODO: Link to the proof.
				Buffer.BlockCopy(sha512Full, 0, sha512256, 0, 32);
			}
			return sha512256;
		}
	}
}
