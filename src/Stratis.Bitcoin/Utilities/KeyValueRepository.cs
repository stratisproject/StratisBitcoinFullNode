using System;
using System.IO;
using System.Text;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;

// TODO add tests

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>Allows saving and loading single values to and from key-value storage.</summary>
    public interface IKeyValueRepository : IDisposable
    {
        void SaveValue<T>(string key, T value);

        T LoadValue<T>(string key);
    }

    public class KeyValueRepository : IKeyValueRepository
    {
        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        private const string TableName = "common";

        public KeyValueRepository(DataFolder dataFolder)
        {
            string folder = dataFolder.AddressManagerFilePath; // TODO change to actual path

            Directory.CreateDirectory(folder);
            this.dbreeze = new DBreezeEngine(folder);
        }

        /// <inheritdoc />
        public void SaveValue<T>(string key, T value)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes(key);

            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                transaction.Insert(TableName, keyBytes, value);
                transaction.Commit();
            }
        }

        /// <inheritdoc />
        public T LoadValue<T>(string key)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes(key);

            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                transaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], T> row = transaction.Select<byte[], T>(TableName, keyBytes);

                if (!row.Exists)
                    return default(T);
                else
                    return row.Value;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze.Dispose();
        }
    }
}
