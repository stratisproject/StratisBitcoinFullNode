using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NBitcoin
{
    public abstract class NoSqlRepository
    {
        private readonly Network network;

        protected NoSqlRepository(Network network = null)
        {
            this.network = network ?? Network.Main;
        }

        public Task PutAsync(string key, IBitcoinSerializable obj)
        {
            return PutBytes(key, obj == null ? null : obj.ToBytes(network: this.network));
        }

        public void Put(string key, IBitcoinSerializable obj)
        {
            PutAsync(key, obj).GetAwaiter().GetResult();
        }

        public async Task<T> GetAsync<T>(string key) where T : IBitcoinSerializable, new()
        {
            var data = await GetBytes(key).ConfigureAwait(false);
            if(data == null)
                return default(T);
            if (!this.network.Consensus.ConsensusFactory.TryCreateNew<T>(out T obj))
                obj = Activator.CreateInstance<T>();
            obj.ReadWrite(data, network: this.network);
            return obj;
        }

        public T Get<T>(string key) where T : IBitcoinSerializable, new()
        {
            return GetAsync<T>(key).GetAwaiter().GetResult();
        }

        public virtual Task PutBatch(IEnumerable<Tuple<string, IBitcoinSerializable>> values)
        {
            return PutBytesBatch(values.Select(s => new Tuple<string, byte[]>(s.Item1, s.Item2 == null ? null : s.Item2.ToBytes())));
        }

        protected abstract Task PutBytesBatch(IEnumerable<Tuple<string, byte[]>> enumerable);
        protected abstract Task<byte[]> GetBytes(string key);

        protected virtual Task PutBytes(string key, byte[] data)
        {
            return PutBytesBatch(new[] { new Tuple<string, byte[]>(key, data) });
        }
    }
}
