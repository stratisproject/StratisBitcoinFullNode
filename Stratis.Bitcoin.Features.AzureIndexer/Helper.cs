using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    internal static class Helper
    {
        internal static List<T> DeserializeList<T>(byte[] bytes) where T : IBitcoinSerializable, new()
        {
            List<T> outpoints = new List<T>();
            if (bytes == null)
                return outpoints;
            MemoryStream ms = new MemoryStream(bytes);
            ms.Position = 0;
            while (ms.Position != ms.Length)
            {
                T outpoint = new T();
                outpoint.ReadWrite(ms, false);
                outpoints.Add(outpoint);
            }
            return outpoints;
        }

        public static byte[] SerializeList<T>(IEnumerable<T> items) where T : IBitcoinSerializable
        {
            MemoryStream ms = new MemoryStream();
            foreach (var item in items)
            {
                item.ReadWrite(ms, true);
            }
            return Helper.GetBytes(ms) ?? new byte[0];
        }

        public static byte[] GetBytes(MemoryStream stream)
        {
            if (stream.Length == 0)
                return null;
            var buffer = stream.GetBuffer();
            Array.Resize(ref buffer, (int)stream.Length);
            return buffer;
        }
        internal static void SetThrottling()
        {
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 1000;
        }

        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary.Add(key, value);
                return true;
            }
            return false;
        }

        const int ColumnMaxSize = 63000;
        internal static void SetEntityProperty(DynamicTableEntity entity, string property, byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            var remaining = data.Length;
            var offset = 0;
            int i = 0;
            while (remaining != 0)
            {
                var chunkSize = Math.Min(ColumnMaxSize, remaining);
                remaining -= chunkSize;

                byte[] chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);
                offset += chunkSize;
                entity.Properties.AddOrReplace(property + i, new EntityProperty(chunk));
                i++;
            }
        }

        internal static byte[] GetEntityProperty(DynamicTableEntity entity, string property)
        {
            List<byte[]> chunks = new List<byte[]>();
            int i = 0;
            while (true)
            {
                if (!entity.Properties.ContainsKey(property + i))
                    break;
                var chunk = entity.Properties[property + i].BinaryValue;
                if (chunk == null || chunk.Length == 0)
                    break;
                chunks.Add(chunk);
                i++;
            }
            byte[] data = new byte[chunks.Sum(o => o.Length)];
            int offset = 0;
            foreach (var chunk in chunks)
            {
                Array.Copy(chunk, 0, data, offset, chunk.Length);
                offset += chunk.Length;
            }
            return data;
        }

        internal static string GetPartitionKey(int bits, byte[] bytes, int startIndex, int length)
        {
            ulong result = 0;
            int remainingBits = bits;
            for (int i = 0 ; i < length ; i++)
            {
                var taken = Math.Min(8, remainingBits);
                ulong inc = (bytes[startIndex + i] & ~(0xFFUL >> taken)) << (i * 8);
                result = result + inc;
                remainingBits -= taken;
                if (remainingBits == 0)
                    break;
            }
            return result.ToString("X2");
        }

        static JsonSerializerSettings _Settings;
        internal static JsonSerializerSettings Settings
        {
            get
            {
                if (_Settings == null)
                {
                    _Settings = new JsonSerializerSettings();
                    _Settings.Converters.Add(new ScriptJsonConverter());
                    _Settings.Converters.Add(new WalletRuleConverter());
                }
                return _Settings;
            }
        }
        internal static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Settings);
        }
        internal static T DeserializeObject<T>(string str)
        {
            return JsonConvert.DeserializeObject<T>(str, Settings);
        }

        public static bool IsError(Exception ex, string code)
        {
			var actualCode = (ex as StorageException)?.RequestInformation?.ExtendedErrorInformation?.ErrorCode;
			return actualCode == code;
		}

        internal static string format = new string(Enumerable.Range(0, int.MaxValue.ToString().Length).Select(c => '0').ToArray());
        static char[] Digit = Enumerable.Range(0, 10).Select(c => c.ToString()[0]).ToArray();

        //Convert '012' to '987'
        internal static string HeightToString(int height)
        {
            var input = height.ToString(format);
            return ToggleChars(input);
        }

        internal static string ToggleChars(string input)
        {
            char[] result = new char[input.Length];
            for (int i = 0 ; i < result.Length ; i++)
            {
                var index = Array.IndexOf(Digit, input[i]);
                result[i] = Digit[Digit.Length - index - 1];
            }
            return new string(result);
        }

        //Convert '987' to '012'
        internal static int StringToHeight(string rowkey)
        {
            return int.Parse(ToggleChars(rowkey));
        }

        public static string GetPartitionKey(int bits, uint nbr)
        {
            var bytes = BitConverter.GetBytes(nbr);
            return GetPartitionKey(bits, bytes, 0, 4);
        }

       
    }
}
