using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// TODO: This code does not seem to be used anywhere in the code base (except for its tests), it does not seem useful to document it,
    /// I suggest to remove it from the codebase completely together with the tests.
    /// </summary>
    public class DBreezeNoSqlRepository : NoSqlRepository, IDisposable
    {
        private DBreezeSingleThreadSession session;
        private string name;
        public DBreezeNoSqlRepository(string name, string folder)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(folder, nameof(folder));

            this.name = name;
            this.session = new DBreezeSingleThreadSession(name, folder);
            this.session.Do(() =>
            {
                this.session.Transaction.ValuesLazyLoadingIsOn = false;
            });
        }


        protected override Task<byte[]> GetBytes(string key)
        {
            Guard.NotEmpty(key, nameof(key));

            return this.session.Do(() =>
            {
                var row = this.session.Transaction.Select<string, byte[]>(this.name, key);
                if (row == null || !row.Exists)
                    return null;
                return row.Value;
            });
        }
        protected override Task PutBytes(string key, byte[] data)
        {
            Guard.NotEmpty(key, nameof(key));
            Guard.NotNull(data, nameof(data));

            return this.session.Do(() =>
            {
                this.session.Transaction.Insert(this.name, key, data);
                this.session.Transaction.Commit();
            });
        }

        protected override async Task PutBytesBatch(IEnumerable<Tuple<string, byte[]>> enumerable)
        {
            Guard.NotNull(enumerable, nameof(enumerable));

            foreach (var kv in enumerable)
            {
                await this.PutBytes(kv.Item1, kv.Item2).ConfigureAwait(false);
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Disposes the object by disposing the underlaying session.
        /// </summary>
        public void Dispose()
        {
            this.session.Dispose();
        }

        #endregion

    }
}
