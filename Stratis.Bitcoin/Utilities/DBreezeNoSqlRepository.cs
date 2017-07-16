using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
	public class DBreezeNoSqlRepository : NoSqlRepository, IDisposable
	{
		DBreezeSingleThreadSession _Session;
		string _Name;
		public DBreezeNoSqlRepository(string name, string folder)
		{
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(folder, nameof(folder));

            this._Name = name;
			this._Session = new DBreezeSingleThreadSession(name, folder);
            this._Session.Do(() =>
			{
                this._Session.Transaction.ValuesLazyLoadingIsOn = false;
			});
		}

		public void Dispose()
		{
            this._Session.Dispose();
		}

		protected override Task<byte[]> GetBytes(string key)
		{
            Guard.NotEmpty(key, nameof(key));

			return this._Session.Do(() =>
			{
				var row = this._Session.Transaction.Select<string, byte[]>(this._Name, key);
				if(row == null || !row.Exists)
					return null;
				return row.Value;
			});
		}
		protected override Task PutBytes(string key, byte[] data)
		{
            Guard.NotEmpty(key, nameof(key));
            Guard.NotNull(data, nameof(data));

            return this._Session.Do(() =>
			{
				this._Session.Transaction.Insert(this._Name, key, data);
                this._Session.Transaction.Commit();
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
	}
}
