using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NBitcoin
{
    public class CachedNoSqlRepository : NoSqlRepository
    {
        private class Raw : IBitcoinSerializable
        {
            public Raw()
            {

            }

            private byte[] _Data = new byte[0];
            public byte[] Data
            {
                get
                {
                    return this._Data;
                }
            }

            #region IBitcoinSerializable Members

            public void ReadWrite(BitcoinStream stream)
            {
                stream.ReadWriteAsVarString(ref this._Data);
            }

            #endregion
        }

        public CachedNoSqlRepository(NoSqlRepository inner) : base(inner.Network)
        {
            this.InnerRepository = inner;
        }

        public NoSqlRepository InnerRepository { get; }

        private Dictionary<string, byte[]> _Table = new Dictionary<string, byte[]>();
        private HashSet<string> _Removed = new HashSet<string>();
        private HashSet<string> _Added = new HashSet<string>();
        private ReaderWriterLock @lock = new ReaderWriterLock();

        public override async Task PutBatch(IEnumerable<Tuple<string, IBitcoinSerializable>> values)
        {
            await base.PutBatch(values).ConfigureAwait(false);
            await this.InnerRepository.PutBatch(values).ConfigureAwait(false);
        }

        protected override Task PutBytesBatch(IEnumerable<Tuple<string, byte[]>> enumerable)
        {
            using (this.@lock.LockWrite())
            {
                foreach (Tuple<string, byte[]> data in enumerable)
                {
                    if (data.Item2 == null)
                    {
                        this._Table.Remove(data.Item1);
                        this._Removed.Add(data.Item1);
                        this._Added.Remove(data.Item1);
                    }
                    else
                    {
                        this._Table.AddOrReplace(data.Item1, data.Item2);
                        this._Removed.Remove(data.Item1);
                        this._Added.Add(data.Item1);
                    }
                }
            }
            return Task.FromResult(true);
        }

        protected override async Task<byte[]> GetBytes(string key)
        {
            byte[] result = null;
            bool found;
            using (this.@lock.LockRead())
            {
                found = this._Table.TryGetValue(key, out result);
            }
            if (!found)
            {
                Raw raw = await this.InnerRepository.GetAsync<Raw>(key).ConfigureAwait(false);
                if (raw != null)
                {
                    result = raw.Data;
                    using (this.@lock.LockWrite())
                    {
                        this._Table.AddOrReplace(key, raw.Data);
                    }
                }
            }
            return result;
        }
    }
}