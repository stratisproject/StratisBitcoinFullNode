using NBitcoin;
using Stratis.Bitcoin.FullNode.Consensus;
using Stratis.Bitcoin.FullNode.BlockPulling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Tests
{
	public class ChainBuilderBlockPuller : BlockPuller
	{
		private readonly ChainBuilder _Builder;

		public ChainBuilderBlockPuller(ChainBuilder builder)
		{
			_Builder = builder;
			SetLocation(_Builder.Chain.Genesis);
		}
		public override Block NextBlock()
		{
			var fork = _Location.FindFork(_Location);
			var nextBlock = _Builder.Chain.EnumerateAfter(fork).First();
			_Location = nextBlock;
			return _Builder._Blocks[nextBlock.HashBlock];
		}

		public override void Reject(Block block, RejectionMode rejectionMode)
		{

		}

		ChainedBlock _Location;
		public override void SetLocation(ChainedBlock location)
		{
			_Location = location;
		}
	}
	public class ChainBuilder
	{
		ConcurrentChain _Chain = new ConcurrentChain();
		Network _Network;
		public ChainBuilder(Network network)
		{
			if(network == null)
				throw new ArgumentNullException("network");
			_Network = network;
			_Chain = new ConcurrentChain(_Network);
			MinerKey = new Key();
			MinerScriptPubKey = MinerKey.PubKey.Hash.ScriptPubKey;
		}

		public ChainBuilderBlockPuller BlockPuller
		{
			get
			{
				return _BlockPuller = _BlockPuller ?? new ChainBuilderBlockPuller(this);
			}
		}

		public ConcurrentChain Chain
		{
			get
			{
				return _Chain;
			}
		}

		public Key MinerKey
		{
			get;
			private set;
		}
		public Script MinerScriptPubKey
		{
			get;
			private set;
		}

		public Transaction Spend(ICoin[] coins, Money amount)
		{
			TransactionBuilder builder = new TransactionBuilder();
			builder.AddCoins(coins);
			builder.AddKeys(MinerKey);
			builder.Send(MinerScriptPubKey, amount);
			builder.SendFees(Money.Coins(0.01m));
			builder.SetChange(MinerScriptPubKey);
			var tx = builder.BuildTransaction(true);
			return tx;
		}

		public ICoin[] GetSpendableCoins()
		{
			return _Blocks
				.Select(b => b.Value)
				.SelectMany(b => b.Transactions.Select(t => new
				{
					Tx = t,
					Block = b
				}))
				.Where(b => !b.Tx.IsCoinBase || (_Chain.Height + 1) - _Chain.GetBlock(b.Block.GetHash()).Height >= 100)
				.Select(b => b.Tx)
				.SelectMany(b => b.Outputs.AsIndexedOutputs())
				.Where(o => o.TxOut.ScriptPubKey == this.MinerScriptPubKey)
				.Select(o => new Coin(o))
				.ToArray();
		}

		public void Mine(int blockCount)
		{
			List<Block> blocks = new List<Block>();
			DateTimeOffset now = DateTimeOffset.UtcNow;
			for(int i = 0; i < blockCount; i++)
			{
				uint nonce = 0;
				Block block = new Block();
				block.Header.HashPrevBlock = _Chain.Tip.HashBlock;
				block.Header.Bits = block.Header.GetWorkRequired(_Network, _Chain.Tip);
				block.Header.UpdateTime(now, _Network, _Chain.Tip);
				var coinbase = new Transaction();
				coinbase.AddInput(TxIn.CreateCoinbase(_Chain.Height + 1));
				coinbase.AddOutput(new TxOut(_Network.GetReward(_Chain.Height + 1), MinerScriptPubKey));
				block.AddTransaction(coinbase);
				foreach(var tx in _Transactions)
				{
					block.AddTransaction(tx);
				}
				block.UpdateMerkleRoot();
				while(!block.CheckProofOfWork())
					block.Header.Nonce = ++nonce;
				block.Header.CacheHashes();
				blocks.Add(block);
				_Transactions.Clear();
				_Chain.SetTip(block.Header);
			}

			foreach(var b in blocks)
			{
				_Blocks.Add(b.GetHash(), b);
			}
		}

		internal Dictionary<uint256, Block> _Blocks = new Dictionary<uint256, Block>();
		private ChainBuilderBlockPuller _BlockPuller;
		private List<Transaction> _Transactions = new List<Transaction>();
		public void Broadcast(Transaction tx)
		{
			_Transactions.Add(tx);
		}
	}
}
