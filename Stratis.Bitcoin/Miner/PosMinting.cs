using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Miner
{

	public class PosMinting
	{
		// Default for -blockmintxfee, which sets the minimum feerate for a transaction in blocks created by mining code 
		public const int DefaultBlockMinTxFee = 1000;
		// Default for -blockmaxsize, which controls the maximum size of block the mining code will create 
		public const int DefaultBlockMaxSize = 750000;
		// Default for -blockmaxweight, which controls the range of block weights the mining code will create 
		public const int DefaultBlockMaxWeight = 3000000;

	    private readonly ConsensusLoop consensusLoop;
	    private readonly ConcurrentChain chain;
	    private readonly Network network;
		private readonly ConnectionManager connection;
		private readonly IDateTimeProvider dateTimeProvider;
	    private readonly BlockAssemblerFactory blockAssemblerFactory;
	    private readonly BlockRepository blockRepository;
	    private readonly ChainBehavior.ChainState chainState;
	    private readonly Signals signals;
	    private readonly FullNode.CancellationProvider cancellationProvider;
		private readonly NodeSettings settings;
		private readonly CachedCoinView cachedCoinView;
		private readonly BlockStoreCache blockStore;
		private readonly StakeChain stakeChain;
		private uint256 hashPrevBlock;
		private Task mining;
		private long lastCoinStakeSearchTime;
		private Money reserveBalance;
		private readonly int minimumInputValue;
		private readonly int minerSleep;

		private PosConsensusValidator posConsensusValidator;
		public long LastCoinStakeSearchInterval;
		public long LastCoinStakeSearchTime;

		public PosMinting(ConsensusLoop consensusLoop, ConcurrentChain chain, Network network, ConnectionManager connection, 
			IDateTimeProvider dateTimeProvider, BlockAssemblerFactory blockAssemblerFactory, BlockRepository blockRepository, 
			BlockStore.ChainBehavior.ChainState chainState, Signals signals, FullNode.CancellationProvider cancellationProvider, 
			NodeSettings settings, CachedCoinView cachedCoinView, BlockStoreCache blockStore, StakeChain stakeChain)
	    {
		    this.consensusLoop = consensusLoop;
		    this.chain = chain;
		    this.network = network;
		    this.connection = connection;
		    this.dateTimeProvider = dateTimeProvider;
		    this.blockAssemblerFactory = blockAssemblerFactory;
		    this.blockRepository = blockRepository;
		    this.chainState = chainState;
		    this.signals = signals;
		    this.cancellationProvider = cancellationProvider;
		    this.settings = settings;
		    this.cachedCoinView = cachedCoinView;
		    this.blockStore = blockStore;
		    this.stakeChain = stakeChain;

			this.minerSleep = 500; // GetArg("-minersleep", 500);
			this.lastCoinStakeSearchTime = Utils.DateTimeToUnixTime(this.dateTimeProvider.GetTimeOffset()); // startup timestamp
		    this.reserveBalance = 0;  // TOOD:settings.ReserveBalance 
		    this.minimumInputValue = 0;

			this.posConsensusValidator = consensusLoop.Validator as PosConsensusValidator;
	    }

		public Task Mine(List<StakeTx> stakeTxes)
		{
			if (this.mining != null)
				return this.mining; // already mining

			this.mining = AsyncLoop.Run("PosMining.Mine", token =>
			{
				this.GenerateBlocks(stakeTxes);
				return Task.CompletedTask;
			},
			cancellationProvider.Cancellation.Token,
			repeatEvery: TimeSpan.FromMilliseconds(this.minerSleep),
			startAfter: TimeSpans.TenSeconds);

			return this.mining;
		}

		public void GenerateBlocks(List<StakeTx> stakeTxes)
	    {
			this.LastCoinStakeSearchInterval = 0;

		    if (this.chain.Tip != this.consensusLoop.Tip)
			    return;

		    BlockTemplate pblocktemplate = null;
			bool tryToSync = true;

		    while (true)
		    {
			    while (!this.connection.ConnectedNodes.Any() || chainState.IsInitialBlockDownload)
			    {
					this.LastCoinStakeSearchInterval = 0;
				    tryToSync = true;
				    this.cancellationProvider.Cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(this.minerSleep));
			    }

			    if (tryToSync)
			    {
				    tryToSync = false;
				    if (this.connection.ConnectedNodes.Count() < 3 ||
				        this.chain.Tip.Header.Time < dateTimeProvider.GetTime() - 10*60)
				    {
					    this.cancellationProvider.Cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(60000));
					    continue;
				    }
			    }

			    if (pblocktemplate == null)
				    pblocktemplate = this.blockAssemblerFactory.CreatePos().CreateNewBlock(new Script());

			    var pblock = pblocktemplate.Block;
			    var pindexPrev = this.chain.Tip;

			    // Trying to sign a block
			    if (this.SignBlock(stakeTxes, pblock, pindexPrev, pblocktemplate.TotalFee))
			    {
					var blockResult = new BlockResult { Block = pblock };
					this.CheckState(new ContextInformation { BlockResult = blockResult }, pindexPrev);

					pblocktemplate = null;
				}
			    else
			    {
					this.cancellationProvider.Cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(this.minerSleep));
				}
		    }
	    }

		private void CheckState(ContextInformation context, ChainedBlock pindexPrev)
		{
			var block = context.BlockResult.Block;

			if (!BlockStake.IsProofOfStake(block))
				return;

			// verify hash target and signature of coinstake tx
			var prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
			if (prevBlockStake == null)
				ConsensusErrors.PrevStakeNull.Throw();

			this.posConsensusValidator.StakeValidator.CheckProofOfStake(context, pindexPrev, prevBlockStake, block.Transactions[1], block.Header.Bits.ToCompact());

			// Found a solution
			if (block.Header.HashPrevBlock != this.chain.Tip.HashBlock)
				return;

			var newChain = new ChainedBlock(block.Header, block.GetHash(), this.chain.Tip);
			this.chain.SetTip(newChain);

			this.consensusLoop.AcceptBlock(context);

			if (context.BlockResult.ChainedBlock == null)
				return; //reorg

			if (context.BlockResult.Error != null)
				return ;

			// similar logic to what's in the full node code
			this.chainState.HighestValidatedPoW = this.consensusLoop.Tip;
			this.signals.Blocks.Broadcast(block);

			// ensure the block is written to disk
			var retry = 0;
			while (++retry < 1000 &&
				   !this.blockRepository.ExistAsync(context.BlockResult.ChainedBlock.HashBlock).GetAwaiter().GetResult())
				Thread.Sleep(100);
		}

		// To decrease granularity of timestamp
		// Supposed to be 2^n-1

		private bool SignBlock(List<StakeTx> stakeTxes, Block block, ChainedBlock pindexBest, long fees)
		{
			// if we are trying to sign
			//    something except proof-of-stake block template
			if (!block.Transactions[0].Outputs[0].IsEmpty)
				return false;

			// if we are trying to sign
			//    a complete proof-of-stake block
			if (BlockStake.IsProofOfStake(block))
				return true;

			Key key = null;
			Transaction txCoinStake = new Transaction();

			txCoinStake.Time  &= ~(uint)BlockValidator.STAKE_TIMESTAMP_MASK;

			long searchTime = txCoinStake.Time; // search to current time


			if (searchTime > this.lastCoinStakeSearchTime)
			//if (DateTime.UtcNow.Second % 16 == 0) // every 16 sedons
			{
				long searchInterval = searchTime - this.lastCoinStakeSearchTime;
				if (this.CreateCoinStake(stakeTxes, pindexBest, block.Header.Bits, searchInterval, fees, ref txCoinStake, ref key))
				{
					if (txCoinStake.Time >= BlockValidator.GetPastTimeLimit(pindexBest) + 1)
					{
						// make sure coinstake would meet timestamp protocol
						//    as it would be the same as the block timestamp
						block.Transactions[0].Time = block.Header.Time = txCoinStake.Time;

						// we have to make sure that we have no future timestamps in
						//    our transactions set
						foreach (var transaction in block.Transactions)
							if (transaction.Time > block.Header.Time)
								block.Transactions.Remove(transaction);

						block.Transactions.Insert(1, txCoinStake);
						block.UpdateMerkleRoot();

						// append a signature to our block
						var signature = key.Sign(block.GetHash());

						block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };
						return true;
					}
				}
				this.LastCoinStakeSearchInterval = searchTime - this.LastCoinStakeSearchTime;
				this.LastCoinStakeSearchTime = searchTime;
			}

			return false;
		}

		public class Output
		{
			public StakeTx StakeTx;
			public int Depth;
		}

		public class StakeTx 
		{
			public uint256 HashBlock;
			public uint256 TransactionHash;
			public TxOut TxOut;
			public int OutputIndex;
			public Transaction Transaction;
			public PubKey PubKey;
			public Key PrvKey;
		}

		public bool CreateCoinStake(List<StakeTx> stakeTxes, ChainedBlock pindexBest, uint bits, long nSearchInterval, long fees, ref Transaction txNew, ref Key key)
		{
			var pindexPrev = pindexBest;
			var bnTargetPerCoinDay = new Target(bits).ToCompact();

			txNew.Inputs.Clear();
			txNew.Outputs.Clear();

			// Mark coin stake transaction
			txNew.Outputs.Add(new TxOut(Money.Zero, new Script()));

			// Choose coins to use
			var nBalance = this.GetBalance(stakeTxes).Satoshi;

			if (nBalance <= this.reserveBalance)
				return false;

			List<StakeTx> vwtxPrev = new List<StakeTx>();

			List<StakeTx> setCoins;
			long nValueIn = 0;

			// Select coins with suitable depth
			if (!SelectCoinsForStaking(stakeTxes, nBalance - this.reserveBalance, txNew.Time, out setCoins, out nValueIn))
				return false;

			//// check if coins are already staking
			//// this is different from the c++ implementation
			//// which pushes the new block to the main chain
			//// and removes it when a longer chain is found
			//foreach (var walletTx in setCoins.ToList())
			//	if (this.minerService.IsStaking(walletTx.TransactionHash, walletTx.OutputIndex))
			//		setCoins.Remove(walletTx);

			if (!setCoins.Any())
				return false;

			long nCredit = 0;
			Script scriptPubKeyKernel = null;

			foreach (var coin in setCoins)
			{
				int maxStakeSearchInterval = 60;
				bool fKernelFound = false;

				for (uint n = 0;
					n < Math.Min(nSearchInterval, maxStakeSearchInterval) 
					&& !fKernelFound 
					&& pindexPrev == this.chain.Tip;
					n++)
				{
					var prevoutStake = new OutPoint(coin.TransactionHash, coin.OutputIndex);
					long nBlockTime = 0;

					var context = new ContextInformation();
					this.posConsensusValidator.StakeValidator.CheckKernel(context, pindexPrev, bits, txNew.Time - n, prevoutStake, ref nBlockTime);
					if (context.Stake.HashProofOfStake != null)
					{
						scriptPubKeyKernel = coin.TxOut.ScriptPubKey;

						key = null;
						// calculate the key type
						if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
						{
							var outPubKey = scriptPubKeyKernel.GetDestinationAddress(this.network);
							key = coin.PrvKey;
						}
						else if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
						{
							var outPubKey = scriptPubKeyKernel.GetDestinationAddress(this.network);
							key = coin.PrvKey;
						}
						else
						{
							//LogPrint("coinstake", "CreateCoinStake : no support for kernel type=%d\n", whichType);
							break; // only support pay to public key and pay to address
						}

						// create a pubkey script form the current script
						var scriptPubKeyOut = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(key.PubKey); //scriptPubKeyKernel;

						txNew.Time -= n;
						txNew.AddInput(new TxIn(prevoutStake));
						nCredit += coin.TxOut.Value;
						vwtxPrev.Add(coin);
						txNew.Outputs.Add(new TxOut(0, scriptPubKeyOut));

						//LogPrint("coinstake", "CreateCoinStake : added kernel type=%d\n", whichType);
						fKernelFound = true;
						break;
					}
				}

				if (fKernelFound)
					break; // if kernel is found stop searching
			}

			if (nCredit == 0 || nCredit > nBalance - this.reserveBalance)
				return false;

			foreach (var coin in setCoins)
			{
				var cointrx = coin;
				//var coinIndex = coin.Value;

				// Attempt to add more inputs
				// Only add coins of the same key/address as kernel
				if (txNew.Outputs.Count == 2
					&& (
						cointrx.TxOut.ScriptPubKey == scriptPubKeyKernel ||
						cointrx.TxOut.ScriptPubKey == txNew.Outputs[1].ScriptPubKey
					)
					&& cointrx.TransactionHash != txNew.Inputs[0].PrevOut.Hash)
				{
					long nTimeWeight = BlockValidator.GetWeight((long)cointrx.Transaction.Time, (long)txNew.Time);

					// Stop adding more inputs if already too many inputs
					if (txNew.Inputs.Count >= 100)
						break;
					// Stop adding inputs if reached reserve limit
					if (nCredit + cointrx.TxOut.Value > nBalance - this.reserveBalance)
						break;
					// Do not add additional significant input
					if (cointrx.TxOut.Value >= GetStakeCombineThreshold())
						continue;
					// Do not add input that is still too young
					if (BlockValidator.IsProtocolV3((int)txNew.Time))
					{
						// properly handled by selection function
					}
					else
					{
						if (nTimeWeight < BlockValidator.StakeMinAge)
							continue;
					}

					txNew.Inputs.Add(new TxIn(new OutPoint(cointrx.TransactionHash, cointrx.OutputIndex)));

					nCredit += cointrx.TxOut.Value;
					vwtxPrev.Add(coin);
				}
			}

			// Calculate coin age reward
			ulong nCoinAge;
			if (!this.posConsensusValidator.StakeValidator.GetCoinAge(this.chain, this.cachedCoinView, txNew, pindexPrev, out nCoinAge))
				return false; //error("CreateCoinStake : failed to calculate coin age");

			long nReward = fees + this.posConsensusValidator.GetProofOfStakeReward(pindexPrev.Height);
			if (nReward <= 0)
				return false;

			nCredit += nReward;

			if (nCredit >= GetStakeSplitThreshold())
				txNew.Outputs.Add(new TxOut(0, txNew.Outputs[1].ScriptPubKey)); //split stake

			// Set output amount
			if (txNew.Outputs.Count == 3)
			{
				txNew.Outputs[1].Value = (nCredit / 2 / BlockValidator.CENT) * BlockValidator.CENT;
				txNew.Outputs[2].Value = nCredit - txNew.Outputs[1].Value;
			}
			else
				txNew.Outputs[1].Value = nCredit;

			// Sign
			int nIn = 0;
			foreach (var walletTx in vwtxPrev)
			{
				if (!SignSignature(new[] { key }, walletTx.Transaction, txNew, nIn++))
					return false; // error("CreateCoinStake : failed to sign coinstake");
			}

			// Limit size
			int nBytes = txNew.GetSerializedSize(ProtocolVersion.ALT_PROTOCOL_VERSION, SerializationType.Network);
			if (nBytes >= MAX_BLOCK_SIZE_GEN / 5)
				return false; // error("CreateCoinStake : exceeded coinstake size limit");

			// Successfully generated coinstake
			return true;
		}

		/** The maximum allowed size for a serialized block, in bytes (network rule) */
		public const int MAX_BLOCK_SIZE = 1000000;
		/** The maximum size for mined blocks */
		public const int MAX_BLOCK_SIZE_GEN = MAX_BLOCK_SIZE / 2;

		private bool SignSignature(Key[] keys, Transaction txFrom, Transaction txTo, int n, params Script[] knownRedeems)
		{
			try
			{
				new TransactionBuilder()
					.AddKeys(keys)
					.AddKnownRedeems(knownRedeems)
					.AddCoins(txFrom)
					.SignTransactionInPlace(txTo);
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

		private static long GetStakeCombineThreshold()
		{
			return 100 * BlockValidator.COIN;
		}

		private static long GetStakeSplitThreshold()
		{
			return 2 * GetStakeCombineThreshold();
		}

		public Money GetBalance(List<StakeTx> stakeTxes)
		{
			var money = new Money(0);
			foreach (var stakeTx in stakeTxes)
			{
				// Must wait until coinbase is safely deep enough in the chain before valuing it
				if ((stakeTx.Transaction.IsCoinBase || stakeTx.Transaction.IsCoinStake) && this.GetBlocksToMaturity(stakeTx) > 0)
					continue;

				money += stakeTx.TxOut.Value;
			}

			return money;
		}

		private bool SelectCoinsForStaking(List<StakeTx> stakeTxes,  long nTargetValue, uint nSpendTime, out List<StakeTx> setCoinsRet, out long nValueRet)
		{
			var coins = this.AvailableCoinsForStaking(stakeTxes, nSpendTime);
			setCoinsRet = new List<StakeTx>();
			nValueRet = 0;

			foreach (var output in coins)
			{
				var pcoin = output.StakeTx;
				//int i = output.Index;

				// Stop if we've chosen enough inputs
				if (nValueRet >= nTargetValue)
					break;

				var n = pcoin.TxOut.Value;

				if (n >= nTargetValue)
				{
					// If input value is greater or equal to target then simply insert
					//    it into the current subset and exit
					setCoinsRet.Add(pcoin);
					nValueRet += n;
					break;
				}
				else if (n < nTargetValue + BlockValidator.CENT)
				{
					setCoinsRet.Add(pcoin);
					nValueRet += n;
				}
			}

			return true;
		}

		private List<Output> AvailableCoinsForStaking(List<StakeTx> stakeTxes, uint nSpendTime)
		{
			var vCoins = new List<Output>();

			foreach (var pcoin in stakeTxes)
			{
				int nDepth = this.GetDepthInMainChain(pcoin);
				if (nDepth < 1)
					continue;

				if (BlockValidator.IsProtocolV3((int)nSpendTime))
				{
					if (nDepth < this.network.Consensus.Option<PosConsensusOptions>().StakeMinConfirmations)
						continue;
				}
				else
				{
					// Filtering by tx timestamp instead of block timestamp may give false positives but never false negatives
					if (pcoin.Transaction.Time + this.network.Consensus.Option<PosConsensusOptions>().StakeMinAge > nSpendTime)
						continue;
				}

				if (this.GetBlocksToMaturity(pcoin) > 0)
					continue;

				if (pcoin.TxOut.Value >= this.minimumInputValue)
				{
					// check if the coin is already staking
					vCoins.Add(new Output { Depth = nDepth, StakeTx = pcoin });
				}
			}

			return vCoins;
		}

		private int GetBlocksToMaturity(StakeTx stakeTx)
		{
			if (!(stakeTx.Transaction.IsCoinBase || stakeTx.Transaction.IsCoinStake))
				return 0;

			return Math.Max(0, (int)this.network.Consensus.Option<PosConsensusOptions>().COINBASE_MATURITY + 1 - this.GetDepthInMainChain(stakeTx));
		}

		// Return depth of transaction in blockchain:
		// -1  : not in blockchain, and not in memory pool (conflicted transaction)
		//  0  : in memory pool, waiting to be included in a block
		// >=1 : this many blocks deep in the main chain
		private int GetDepthInMainChain(StakeTx stakeTx)
		{
			var chainedBlock = this.chain.GetBlock(stakeTx.HashBlock);

			if (chainedBlock == null)
				return -1;

			// TODO: check if in memory pool then return 0

			return this.chain.Tip.Height - chainedBlock.Height + 1;
		}
	}
}
