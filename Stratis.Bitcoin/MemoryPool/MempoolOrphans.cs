using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
    public class MempoolOrphans
    {
		// Expiration time for orphan transactions in seconds 
		const long ORPHAN_TX_EXPIRE_TIME = 20 * 60;
		// Default for -maxorphantx, maximum number of orphan transactions kept in memory 
		public const int DEFAULT_MAX_ORPHAN_TRANSACTIONS = 100;
		// Minimum time between orphan transactions expire time checks in seconds 
		public const int ORPHAN_TX_EXPIRE_INTERVAL = 5 * 60;

		public MempoolScheduler MempoolScheduler { get; }
		public MempoolValidator Validator { get; } // public for testing
		private readonly TxMempool memPool;
		private readonly ConcurrentChain chain;
		private readonly CoinView coinView;
	    private readonly DateTimeProvider dateTimeProvider;
	    private readonly NodeSettings nodeArgs;

		private readonly Dictionary<uint256, OrphanTx> mapOrphanTransactions;
		private readonly Dictionary<OutPoint, List<OrphanTx>> mapOrphanTransactionsByPrev;
		private readonly Dictionary<uint256, uint256> recentRejects;
		private long nNextSweep;
	    private readonly Random random = new Random();
		private uint256 hashRecentRejectsChainTip;

		public MempoolOrphans(MempoolScheduler mempoolScheduler, TxMempool memPool, ConcurrentChain chain, 
			MempoolValidator validator, CoinView coinView, DateTimeProvider dateTimeProvider, NodeSettings nodeArgs)
		{
			this.MempoolScheduler = mempoolScheduler;
			this.memPool = memPool;
			this.chain = chain;
			this.coinView = coinView;
			this.dateTimeProvider = dateTimeProvider;
			this.nodeArgs = nodeArgs;
			this.Validator = validator;

			this.mapOrphanTransactions = new Dictionary<uint256, OrphanTx>();
			this.mapOrphanTransactionsByPrev = new Dictionary<OutPoint, List<OrphanTx>>(); // OutPoint already correctly implements equality compare
			this.recentRejects = new Dictionary<uint256, uint256>();
			this.hashRecentRejectsChainTip = uint256.Zero;
		}

		public class OrphanTx
		{
			// When modifying, adapt the copy of this definition in tests/DoS_tests.
			public Transaction Tx;
			public ulong NodeId;
			public long TimeExpire;
		};

	    public List<OrphanTx> OrphansList() // for testing
	    {
		    return this.mapOrphanTransactions.Values.ToList();
	    }

		public async Task<bool> AlreadyHave(uint256 trxid)
		{
			if (this.chain.Tip.HashBlock != hashRecentRejectsChainTip)
			{
				await this.MempoolScheduler.WriteAsync(() =>
				{
					// If the chain tip has changed previously rejected transactions
					// might be now valid, e.g. due to a nLockTime'd tx becoming valid,
					// or a double-spend. Reset the rejects filter and give those
					// txs a second chance.
					hashRecentRejectsChainTip = this.chain.Tip.HashBlock;
					recentRejects.Clear();
				});
			}

			// Use pcoinsTip->HaveCoinsInCache as a quick approximation to exclude
			// requesting or processing some txs which have already been included in a block
			return await this.MempoolScheduler.ReadAsync(() => 
								recentRejects.ContainsKey(trxid) ||
								this.memPool.Exists(trxid) ||
								this.mapOrphanTransactions.ContainsKey(trxid));
		}

		public async Task ProcessesOrphans(MempoolBehavior behavior, Transaction tx)
	    {
			Queue<OutPoint> vWorkQueue = new Queue<OutPoint>();
			List<uint256> vEraseQueue = new List<uint256>();
			
			var trxHash = tx.GetHash();
		    for (var index = 0; index < tx.Outputs.Count; index++)
			    vWorkQueue.Enqueue(new OutPoint(trxHash, index));
			
			// Recursively process any orphan transactions that depended on this one
			List<ulong> setMisbehaving = new List<ulong>();
			while (vWorkQueue.Any())
			{
				var itByPrev = await this.MempoolScheduler.ReadAsync(() => this.mapOrphanTransactionsByPrev.TryGet(vWorkQueue.Dequeue()));
				if (itByPrev == null)
					continue;

				foreach (var mi in itByPrev)
				{
					var orphanTx = mi.Tx; //->second.tx;
					var  orphanHash = orphanTx.GetHash();
					var fromPeer = mi.NodeId;// (*mi)->second.fromPeer;

					if (setMisbehaving.Contains(fromPeer))
						continue;

					// Use a dummy CValidationState so someone can't setup nodes to counter-DoS based on orphan
					// resolution (that is, feeding people an invalid transaction based on LegitTxX in order to get
					// anyone relaying LegitTxX banned)
					MempoolValidationState stateDummy = new MempoolValidationState(true);
					if (await this.Validator.AcceptToMemoryPool(stateDummy, orphanTx))
					{
						Logging.Logs.Mempool.LogInformation($"accepted orphan tx {orphanHash}");
						await behavior.RelayTransaction(orphanTx.GetHash());
						for (var index = 0; index < orphanTx.Outputs.Count; index++)
							vWorkQueue.Enqueue(new OutPoint(orphanHash, index));
						vEraseQueue.Add(orphanHash);
					}
					else if (!stateDummy.MissingInputs)
					{
						int nDos = 0;
						if (stateDummy.IsInvalid && nDos > 0)
						{
							// Punish peer that gave us an invalid orphan tx
							//Misbehaving(fromPeer, nDos);
							setMisbehaving.Add(fromPeer);
							Logging.Logs.Mempool.LogInformation($"invalid orphan tx {orphanHash}");
						}
						// Has inputs but not accepted to mempool
						// Probably non-standard or insufficient fee/priority
						Logging.Logs.Mempool.LogInformation($"removed orphan tx {orphanHash}");
						vEraseQueue.Add(orphanHash);
						if (!orphanTx.HasWitness && !stateDummy.CorruptionPossible)
						{
							// Do not use rejection cache for witness transactions or
							// witness-stripped transactions, as they can have been malleated.
							// See https://github.com/bitcoin/bitcoin/issues/8279 for details.
							await this.MempoolScheduler.WriteAsync(() => this.recentRejects.TryAdd(orphanHash, orphanHash));
						}
					}
					this.memPool.Check(new MempoolCoinView(this.coinView, this.memPool, this.MempoolScheduler));
				}
			}

		    foreach (var hash in vEraseQueue)
				await this.EraseOrphanTx(hash);
		}

	    public async Task<bool> ProcessesOrphansMissingInputs(Node from, Transaction tx)
	    {
		    // It may be the case that the orphans parents have all been rejected
		    var rejectedParents = await this.MempoolScheduler.ReadAsync(() =>
		    {
			    return tx.Inputs.Any(txin => this.recentRejects.ContainsKey(txin.PrevOut.Hash));
		    });

		    if (rejectedParents)
		    {
			    Logging.Logs.Mempool.LogInformation($"not keeping orphan with rejected parents {tx.GetHash()}");
			    return false;
		    }

		    int nFetchFlags = 0; //GetFetchFlags(pfrom, chainActive.Tip(), chainparams.GetConsensus());
		    foreach (var txin in tx.Inputs)
		    {
			    // TODO: this goes in the RelayBehaviour
			    //CInv _inv(MSG_TX | nFetchFlags, txin.prevout.hash);
			    //behavior.AttachedNode.Behaviors.Find<RelayBehaviour>() pfrom->AddInventoryKnown(_inv);
			    //if (!await this.AlreadyHave(txin.PrevOut.Hash))
			    //	from. pfrom->AskFor(_inv);
		    }
		    var ret = await this.AddOrphanTx(from.PeerVersion.Nonce, tx);

		    // DoS prevention: do not allow mapOrphanTransactions to grow unbounded
		    int nMaxOrphanTx = this.nodeArgs.Mempool.MaxOrphanTx;
		    int nEvicted = await this.LimitOrphanTxSize(nMaxOrphanTx);
		    if (nEvicted > 0)
			    Logging.Logs.Mempool.LogInformation($"mapOrphan overflow, removed {nEvicted} tx");

		    return ret;
	    }

	    public async Task<int> LimitOrphanTxSize(int maxOrphanTx)
	    {
			int nEvicted = 0;
			var nNow = this.dateTimeProvider.GetTime();
			if (nNextSweep <= nNow)
			{
				// Sweep out expired orphan pool entries:
				int nErased = 0;
				var nMinExpTime = nNow + ORPHAN_TX_EXPIRE_TIME - ORPHAN_TX_EXPIRE_INTERVAL;

				var orphansValues = await this.MempoolScheduler.ReadAsync(() => this.mapOrphanTransactions.Values.ToList());
				foreach (var maybeErase in orphansValues) // create a new list as this will be removing items from the dictionary
				{
					if (maybeErase.TimeExpire <= nNow)
					{
						nErased += await this.EraseOrphanTx(maybeErase.Tx.GetHash()) ? 1 : 0;
					}
					else
					{
						nMinExpTime = Math.Min(maybeErase.TimeExpire, nMinExpTime);
					}
				}
				// Sweep again 5 minutes after the next entry that expires in order to batch the linear scan.
				nNextSweep = nMinExpTime + ORPHAN_TX_EXPIRE_INTERVAL;
				if (nErased > 0)
					Logging.Logs.Mempool.LogInformation($"Erased {nErased} orphan tx due to expiration");
			}
		    await await this.MempoolScheduler.ReadAsync(async () => 
		    {
				while (this.mapOrphanTransactions.Count > maxOrphanTx)
				{
					// Evict a random orphan:
					var randomCount = random.Next(this.mapOrphanTransactions.Count);
					var erase = this.mapOrphanTransactions.ElementAt(randomCount).Key;
					await EraseOrphanTx(erase); // this will split the loop between the concurrent and exclusive scheduler
					++nEvicted;
				}
			});
			
			return nEvicted;
		}
	  
		public Task<bool> AddOrphanTx(ulong nodeId, Transaction tx)
		{
			return this.MempoolScheduler.WriteAsync(() =>
			{
				var hash = tx.GetHash();
				if (this.mapOrphanTransactions.ContainsKey(hash))
					return false;

				// Ignore big transactions, to avoid a
				// send-big-orphans memory exhaustion attack. If a peer has a legitimate
				// large transaction with a missing parent then we assume
				// it will rebroadcast it later, after the parent transaction(s)
				// have been mined or received.
				// 100 orphans, each of which is at most 99,999 bytes big is
				// at most 10 megabytes of orphans and somewhat more byprev index (in the worst case):
				var sz = MempoolValidator.GetTransactionWeight(tx);
				if (sz >= ConsensusValidator.MAX_STANDARD_TX_WEIGHT)
				{
					Logging.Logs.Mempool.LogInformation($"ignoring large orphan tx (size: {sz}, hash: {hash})");
					return false;
				}

				var orphan = new OrphanTx
				{
					Tx = tx,
					NodeId = nodeId,
					TimeExpire = this.dateTimeProvider.GetTime() + ORPHAN_TX_EXPIRE_TIME
				};
				if (this.mapOrphanTransactions.TryAdd(hash, orphan))
				{
					foreach (var txin in tx.Inputs)
					{
						var prv = this.mapOrphanTransactionsByPrev.TryGet(txin.PrevOut);
						if (prv == null)
						{
							prv = new List<OrphanTx>();
							this.mapOrphanTransactionsByPrev.Add(txin.PrevOut, prv);
						}
						prv.Add(orphan);
					}
				}

				var orphanSize = this.mapOrphanTransactions.Count;
				Logging.Logs.Mempool.LogInformation(
					$"stored orphan tx {hash} (mapsz {orphanSize} outsz {this.mapOrphanTransactionsByPrev.Count})");
				this.Validator.PerformanceCounter.SetMempoolOrphanSize(orphanSize);

				return true;
			});
		}

		private Task<bool> EraseOrphanTx(uint256 hash)
		{
			return this.MempoolScheduler.WriteAsync(() =>
			{
				var it = this.mapOrphanTransactions.TryGet(hash);
				if (it == null)
					return false;
				foreach (var txin in it.Tx.Inputs)
				{
					var itPrev = this.mapOrphanTransactionsByPrev.TryGet(txin.PrevOut);
					if (itPrev == null)
						continue;
					itPrev.Remove(it);
					if (!itPrev.Any())
						this.mapOrphanTransactionsByPrev.Remove(txin.PrevOut);
				}
				this.mapOrphanTransactions.Remove(hash);
				return true;
			});
		}


		public Task EraseOrphansFor(ulong peer)
		{
			return this.MempoolScheduler.ReadAsync(async () =>
			{
				int erased = 0;
				foreach (var erase in this.mapOrphanTransactions.Values.Where(w => w.NodeId == peer).ToList())
					erased += await EraseOrphanTx(erase.Tx.GetHash()) ? 1 : 0;

				if (erased > 0)
					Logging.Logs.Mempool.LogInformation($"Erased {erased} orphan tx from peer {peer}");

				//return true;
			}).Unwrap();
		}
	}
}
