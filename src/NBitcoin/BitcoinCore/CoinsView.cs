using System;
using System.Linq;
using System.Threading.Tasks;

namespace NBitcoin.BitcoinCore
{
    public class CoinsView
    {
        public CoinsView(NoSqlRepository noSqlRepository)
        {
            this.NoSqlRepository = noSqlRepository ?? throw new ArgumentNullException(nameof(noSqlRepository));
        }

        public CoinsView(Network network)
            : this(new InMemoryNoSqlRepository(network))
        {
        }

        public NoSqlRepository NoSqlRepository { get; }

        public Coins GetCoins(uint256 txId)
        {
            return this.NoSqlRepository.GetAsync<Coins>(txId.ToString()).GetAwaiter().GetResult();
        }

        public Task<Coins> GetCoinsAsync(uint256 txId)
        {
            return this.NoSqlRepository.GetAsync<Coins>(txId.ToString());
        }

        public void SetCoins(uint256 txId, Coins coins)
        {
            this.NoSqlRepository.PutAsync(txId.ToString(), coins);
        }

        public bool HaveCoins(uint256 txId)
        {
            return GetCoins(txId) != null;
        }

        public uint256 GetBestBlock()
        {
            return GetBestBlockAsync().GetAwaiter().GetResult();
        }

        public async Task<uint256> GetBestBlockAsync()
        {
            uint256.MutableUint256 block = await this.NoSqlRepository.GetAsync<uint256.MutableUint256>("B").ConfigureAwait(false);
            return block == null ? uint256.Zero : block.Value;
        }

        public void SetBestBlock(uint256 blockId)
        {
            this.NoSqlRepository.PutAsync("B", blockId.AsBitcoinSerializable());
        }

        public bool HaveInputs(Transaction tx)
        {
            if (!tx.IsCoinBase)
            {
                // first check whether information about the prevout hash is available
                foreach (TxIn input in tx.Inputs)
                {
                    OutPoint prevout = input.PrevOut;
                    if (!HaveCoins(prevout.Hash))
                        return false;
                }

                // then check whether the actual outputs are available
                foreach (TxIn input in tx.Inputs)
                {
                    OutPoint prevout = input.PrevOut;
                    Coins coins = GetCoins(prevout.Hash);
                    if (!coins.IsAvailable(prevout.N))
                        return false;
                }
            }

            return true;
        }

        public TxOut GetOutputFor(TxIn input)
        {
            Coins coins = GetCoins(input.PrevOut.Hash);
            return coins.TryGetOutput(input.PrevOut.N);
        }

        public Money GetValueIn(Transaction tx)
        {
            if (tx.IsCoinBase)
                return 0;

            return tx.Inputs.Select(i => GetOutputFor(i).Value).Sum();
        }

        public CoinsView CreateCached()
        {
            return new CoinsView(new CachedNoSqlRepository(this.NoSqlRepository));
        }

        public void AddTransaction(IConsensus consensus, Transaction tx, int height)
        {
            SetCoins(tx.GetHash(), new Coins(tx, height));
        }
    }
}