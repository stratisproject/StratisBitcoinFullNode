using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC.Models
{
	/// <summary>
	/// Data structure for RPC blocks.
	/// <see cref="https://bitcoin.org/en/developer-reference#getblock"/>
	/// </summary>
	public class BlockModel
	{

		int _notImplemented = -99;
		uint _notImplemented2 = 0;
		/// <summary>
		/// Constructs a RPC BlockModel from a block header object.
		/// </summary>
		/// <param name="chainedBlock">The block header.</param>
		public BlockModel(ChainedBlock chainedBlock, ChainedBlock nextChainedBlock, Block block)
		{
			Guard.NotNull(chainedBlock, nameof(chainedBlock));

			this.Hash = chainedBlock.Header.GetHash().ToString();

			// TODO
			this.Confirmations = _notImplemented;
			this.Size = _notImplemented2;
			this.StrippedSize = _notImplemented2;
			this.Weight = _notImplemented2;
			this.MedianTime = _notImplemented2;
			this.Difficulty = _notImplemented2;

			this.Chainwork = chainedBlock.ChainWork.ToString();

			this.Height = (uint)chainedBlock.Height;
			this.Version = (uint)chainedBlock.Header.Version;
			this.VersionHex = $"0x0{Version}";
			this.MerkleRoot = chainedBlock.Header.HashMerkleRoot.ToString();

			this.Tx = block.Transactions.Select(t => t.GetHash().ToString()).ToArray();
			this.Time = block.Header.Time;
			this.Nonce = (int)chainedBlock.Header.Nonce;

			byte[] bitBytes = this.GetBytes(chainedBlock.Header.Bits.ToCompact());
			string encodedBitBytes = Encoders.Hex.EncodeData(bitBytes);
			this.Bits = encodedBitBytes;

			this.PreviousBlockHash = chainedBlock.Header.HashPrevBlock.ToString();
			if (nextChainedBlock != null)
				this.NextBlockHash = nextChainedBlock.Header.GetHash().ToString();

		}

		/// <summary>
		/// The block' hash.
		/// </summary>
		[JsonProperty("hash")]
		public string Hash { get; set; }

		/// <summary>
		/// The number of confirmations the transactions in this block have, starting at 1 when this block is at the tip of 
		/// the best block chain. This score will be -1 if the the block is not part of the best block chain.
		/// </summary>
		[JsonProperty("confirmations")]
		public int Confirmations { get; set; }

		/// <summary>
		/// The size of this block in serialized block format, counted in bytes.
		/// </summary>
		[JsonProperty("size")]
		public uint Size { get; set; }

		/// <summary>
		/// Added in Bitcoin Core 0.13.0
		/// The size of this block in serialized block format excluding witness data, counted in bytes.
		/// </summary>
		[JsonProperty("strippedsize")]
		public uint StrippedSize { get; set; }

		/// <summary>
		/// Added in Bitcoin Core 0.13.0
		/// This block’s weight as defined in BIP141.
		/// </summary>
		[JsonProperty("weight")]
		public uint Weight { get; set; }

		/// <summary>
		/// The height of this block on its block chain.
		/// </summary>
		[JsonProperty("height")]
		public uint Height { get; set; }

		/// <summary>
		/// The blocks version number.
		/// </summary>
		[JsonProperty("version")]
		public uint Version { get; private set; }

		/// <summary>
		/// Added in Bitcoin Core 0.13.0
		/// This block’s version formatted in hexadecimal
		/// </summary>
		[JsonProperty("versionHex")]
		public string VersionHex { get; private set; }

		/// <summary>
		/// The merkle root for this block encoded as hex in RPC byte order.
		/// </summary>
		[JsonProperty("merkleroot")]
		public string MerkleRoot { get; private set; }

		/// <summary>
		/// An array containing the TXIDs of all transactions in this block. 
		/// The transactions appear in the array in the same order they appear in the serialized block.
		/// </summary>
		[JsonProperty("tx")]
		public string[] Tx { get; private set; }

		/// <summary>
		/// The block time in seconds since epoch (Jan 1 1970 GMT).
		/// </summary>
		[JsonProperty("time")]
		public uint Time { get; private set; }

		/// <summary>
		/// Added in Bitcoin Core 0.12.0
		/// The median block time in Unix epoch time
		/// </summary>
		[JsonProperty("mediantime")]
		public uint MedianTime { get; private set; }

		/// <summary>
		/// The nonce which was successful at turning this particular block
		/// into one that could be added to the best block chain.
		/// </summary>
		[JsonProperty("nonce")]
		public int Nonce { get; private set; }

		/// <summary>
		/// The target threshold this block's header had to pass.
		/// </summary>
		[JsonProperty("bits")]
		public string Bits { get; private set; }

		/// <summary>
		/// The estimated amount of work done to find this block relative to the estimated amount of work done to find block 0.
		/// </summary>
		[JsonProperty("difficulty")]
		public double Difficulty { get; set; }

		/// <summary>
		/// The estimated number of block header hashes miners had to check from the genesis block to this block, encoded as big-endian hex.
		/// </summary>
		[JsonProperty("chainwork")]
		public string Chainwork { get; set; }

		/// <summary>
		/// The hash of the header of the previous block,
		/// encoded as hex in RPC byte order.
		/// </summary>
		[JsonProperty("previousblockhash")]
		public string PreviousBlockHash { get; private set; }

		/// <summary>
		/// The hash of the header of the previous block,
		/// encoded as hex in RPC byte order.
		/// </summary>
		[JsonProperty("nextblockhash")]
		public string NextBlockHash { get; private set; }



		/// <summary>
		/// Convert compact of miner challenge to byte format,
		/// serialized for transmission via RPC.
		/// </summary>
		/// <param name="compact">Compact representation of challenge.</param>
		/// <returns>Byte representation of challenge.</returns>
		/// <seealso cref="Target"/>
		private byte[] GetBytes(uint compact)
		{
			return new byte[]
			{
				(byte)(compact >> 24),
				(byte)(compact >> 16),
				(byte)(compact >> 8),
				(byte)(compact)
			};
		}
	}
}
