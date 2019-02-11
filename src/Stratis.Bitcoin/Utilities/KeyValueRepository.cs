using System;
using System.IO;
using System.Text;
using DBreeze;
using DBreeze.DataTypes;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>Allows saving and loading single values to and from key-value storage.</summary>
    public interface IKeyValueRepository : IDisposable
    {
        /// <summary>Persists byte array to the database.</summary>
        void SaveBytes(string key, byte[] bytes);

        /// <summary>Persists any object that <see cref="DBreezeSerializer"/> can serialize to the database.</summary>
        void SaveValue<T>(string key, T value);

        /// <summary>Persists any object to the database. Object is stored as JSON.</summary>
        void SaveValueJson<T>(string key, T value);

        /// <summary>Loads byte array from the database.</summary>
        byte[] LoadBytes(string key);

        /// <summary>Loads an object that <see cref="DBreezeSerializer"/> can deserialize from the database.</summary>
        T LoadValue<T>(string key);

        /// <summary>Loads JSON from the database and deserializes it.</summary>
        T LoadValueJson<T>(string key);
    }

    public class KeyValueRepository : IKeyValueRepository
    {
        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        private const string TableName = "common";

        private readonly DBreezeSerializer dBreezeSerializer;

        public KeyValueRepository(DataFolder dataFolder, DBreezeSerializer dBreezeSerializer) : this (dataFolder.KeyValueRepositoryPath, dBreezeSerializer)
        {
        }

        public KeyValueRepository(string folder, DBreezeSerializer dBreezeSerializer)
        {
            Directory.CreateDirectory(folder);
            this.dbreeze = new DBreezeEngine(folder);
            this.dBreezeSerializer = dBreezeSerializer;
        }

        /// <inheritdoc />
        public void SaveBytes(string key, byte[] bytes)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes(key);

            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                transaction.Insert<byte[], byte[]>(TableName, keyBytes, bytes);

                transaction.Commit();
            }
        }

        /// <inheritdoc />
        public void SaveValue<T>(string key, T value)
        {
            this.SaveBytes(key, this.dBreezeSerializer.Serialize(value));
        }

        /// <inheritdoc />
        public void SaveValueJson<T>(string key, T value)
        {
            string json = Serializer.ToString(value);
            byte[] jsonBytes = Encoding.ASCII.GetBytes(json);

            this.SaveBytes(key, jsonBytes);
        }

        /// <inheritdoc />
        public byte[] LoadBytes(string key)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes(key);

            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                transaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>(TableName, keyBytes);

                if (!row.Exists)
                    return null;

                return row.Value;
            }
        }

        /// <inheritdoc />
        public T LoadValue<T>(string key)
        {
            byte[] bytes = this.LoadBytes(key);

            if (bytes == null)
                return default(T);

            T value = this.dBreezeSerializer.Deserialize<T>(bytes);
            return value;
        }

        /// <inheritdoc />
        public T LoadValueJson<T>(string key)
        {
            byte[] bytes = this.LoadBytes(key);

            if (bytes == null)
                return default(T);

            string json = Encoding.ASCII.GetString(bytes);

            T value = Serializer.ToObject<T>(json);

            return value;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze.Dispose();
        }
    }
}
