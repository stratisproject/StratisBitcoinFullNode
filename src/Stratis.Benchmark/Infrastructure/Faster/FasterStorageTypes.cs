using System;
using System.Text;
using FASTER.core;

namespace Stratis.Benchmark.Infrastructure.Faster
{
    public class CacheKey : IFasterEqualityComparer<CacheKey>
    {
        public string Key;
        public string Table;

       public string PartitionedKey => $"{this.Table}_{this.Key}";

        public CacheKey()
        {
        }

        public CacheKey(string table, string key)
        {
            this.Key = key;
            this.Table = table;
        }

        public long GetHashCode64(ref CacheKey k)
        {
            long hashCode = 0;
            if (!string.IsNullOrEmpty(k.PartitionedKey))
            {
                byte[] byteContents = Encoding.Unicode.GetBytes(k.PartitionedKey);
                System.Security.Cryptography.SHA256 hash = 
                    new System.Security.Cryptography.SHA256CryptoServiceProvider();
                byte[] hashText = hash.ComputeHash(byteContents);
                long hashCodeStart = BitConverter.ToInt64(hashText, 0);
                long hashCodeMedium = BitConverter.ToInt64(hashText, 8);
                long hashCodeEnd = BitConverter.ToInt64(hashText, 24);
                hashCode = hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;
            }

            return hashCode;
        }

        public static (string table, string key) ParseKey(string combinedKey)
        {
            if (string.IsNullOrEmpty(combinedKey)) 
                throw new ArgumentNullException(nameof(combinedKey));

            var tokens = combinedKey.Split('_');
            if (tokens.Length < 2) 
                throw new ArgumentException(nameof(combinedKey));

            return (tokens[0], tokens[1]);
        }

        public bool Equals(ref CacheKey k1, ref CacheKey k2)
        {
            return k1.PartitionedKey == k2.PartitionedKey;
        }
    }

    public class CacheKeySerializer : BinaryObjectSerializer<CacheKey>
    {
        public override void Deserialize(ref CacheKey obj)
        {
            string combinedKey = this.reader.ReadString();
            (string table, string key) = CacheKey.ParseKey(combinedKey);
            obj.Key = key;
            obj.Table = table;
        }

        public override void Serialize(ref CacheKey obj)
        {
            this.writer.Write(obj.PartitionedKey);
        }
    }

    public class CacheValue
    {
        public byte[] Value;

        public CacheValue()
        {
        }

        public CacheValue(byte[] first)
        {
            this.Value = first;
        }
    }

    public class CacheValueSerializer : BinaryObjectSerializer<CacheValue>
    {
        public override void Deserialize(ref CacheValue obj)
        {
            obj.Value = Encoding.UTF8.GetBytes(this.reader.ReadString());
        }

        public override void Serialize(ref CacheValue obj)
        {
            this.writer.Write(Encoding.UTF8.GetString(obj.Value));
        }
    }

    public struct CacheInput
    {
    }

    public struct CacheOutput
    {
        public CacheValue Value;
    }

    public class CacheFunctions : IFunctions<CacheKey, CacheValue, CacheInput, CacheOutput, Empty>
    {
        public void ConcurrentReader(ref CacheKey key, ref CacheInput input, ref CacheValue value, ref CacheOutput dst)
        {
            dst.Value = value;
        }

        public void ConcurrentWriter(ref CacheKey key, ref CacheValue src, ref CacheValue dst)
        {
            dst = src;
        }

        public void CopyUpdater(ref CacheKey key, ref CacheInput input, ref CacheValue oldValue, ref CacheValue newValue)
        {
        }

        public void InitialUpdater(ref CacheKey key, ref CacheInput input, ref CacheValue value)
        {
        }

        public void InPlaceUpdater(ref CacheKey key, ref CacheInput input, ref CacheValue value)
        {
        }

        public void CheckpointCompletionCallback(Guid sessionId, long serialNum)
        {
        }

        public void ReadCompletionCallback(ref CacheKey key, ref CacheInput input, ref CacheOutput output, Empty ctx, Status status)
        {
        }

        public void RMWCompletionCallback(ref CacheKey key, ref CacheInput input, Empty ctx, Status status)
        {
        }

        public void SingleReader(ref CacheKey key, ref CacheInput input, ref CacheValue value, ref CacheOutput dst)
        {
            dst.Value = value;
        }

        public void SingleWriter(ref CacheKey key, ref CacheValue src, ref CacheValue dst)
        {
            dst = src;
        }

        public void UpsertCompletionCallback(ref CacheKey key, ref CacheValue value, Empty ctx)
        {
        }
    }
}