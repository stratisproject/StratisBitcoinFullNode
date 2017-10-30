using Microsoft.Data.OData;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using NBitcoin.Crypto;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public static class Extensions
    {
        public static IEnumerable<T> Distinct<T, TComparer>(this IEnumerable<T> input, Func<T, TComparer> comparer)
        {
            return input.Distinct(new AnonymousEqualityComparer<T, TComparer>(comparer));
        }


        public static CoinCollection SelectSpentCoins(this IEnumerable<OrderedBalanceChange> entries)
        {
            return SelectSpentCoins(entries, true);
        }

        public static CoinCollection SelectUnspentCoins(this IEnumerable<OrderedBalanceChange> entries)
        {
            return SelectSpentCoins(entries, false);
        }

        private static CoinCollection SelectSpentCoins(IEnumerable<OrderedBalanceChange> entries, bool spent)
        {
            CoinCollection result = new CoinCollection();
            Dictionary<OutPoint, ICoin> spentCoins = new Dictionary<OutPoint, ICoin>();
            Dictionary<OutPoint, ICoin> receivedCoins = new Dictionary<OutPoint, ICoin>();
            foreach(var entry in entries)
            {
                if(entry.SpentCoins != null)
                {
                    foreach(var c in entry.SpentCoins)
                    {
                        spentCoins.AddOrReplace(c.Outpoint, c);
                    }
                }
                foreach(var c in entry.ReceivedCoins)
                {
                    receivedCoins.AddOrReplace(c.Outpoint, c);
                }
            }
            if(spent)
            {
                result.AddRange(spentCoins.Values.Select(s => s));
            }
            else
            {
                result.AddRange(receivedCoins.Where(r => !spentCoins.ContainsKey(r.Key)).Select(kv => kv.Value));
            }
            return result;
        }

        /// <summary>
        /// Remove unconfirmed for 30minutes
        /// </summary>
        /// <typeparam name="TBalanceChangeEntry"></typeparam>
        /// <param name="entries"></param>
        /// <returns></returns>
        public static IEnumerable<OrderedBalanceChange> WhereNotExpired(this IEnumerable<OrderedBalanceChange> entries)
        {
            return WhereNotExpired(entries, TimeSpan.FromMinutes(30));
        }

        /// <summary>
        /// Remove unconfirmed entries for expiration
        /// </summary>
        /// <typeparam name="TBalanceChangeEntry"></typeparam>
        /// <param name="entries"></param>
        /// <param name="expiration"></param>
        /// <returns></returns>
        public static IEnumerable<OrderedBalanceChange> WhereNotExpired(this IEnumerable<OrderedBalanceChange> entries, TimeSpan expiration)
        {
            return entries
                       .Where(e =>
                           e.BlockId != null ||
                           (e.BlockId == null
                                   &&
                                    (DateTime.UtcNow - e.SeenUtc) < expiration));
        }

        public static IEnumerable<OrderedBalanceChange> WhereConfirmed(this IEnumerable<OrderedBalanceChange> entries, ChainBase chain, int minConfirmation = 1)
        {
            return
                entries
                .Where(e => IsMinConf(e, minConfirmation, chain));
        }

        public static BalanceSheet AsBalanceSheet(this IEnumerable<OrderedBalanceChange> entries, ChainBase chain)
        {
            return new BalanceSheet(entries, chain);
        }

        private static bool IsMinConf(OrderedBalanceChange e, int minConfirmation, ChainBase chain)
        {
            if(e.BlockId == null)
                return minConfirmation == 0;

            var b = chain.GetBlock(e.BlockId);
            if(b == null)
                return false;
            return (chain.Height - b.Height) + 1 >= minConfirmation;
        }

        public static void MakeFat(this DynamicTableEntity entity, int size)
        {
            entity.Properties.Clear();
            entity.Properties.Add("fat", new EntityProperty(size));
        }
        public static bool IsFat(this DynamicTableEntity entity)
        {
            return entity.Properties.Any(p => p.Key.Equals("fat", StringComparison.OrdinalIgnoreCase) &&
                                              p.Value.PropertyType == EdmType.Int32);
        }
        public static string GetFatBlobName(this ITableEntity entity)
        {
            return "unk" + Hashes.Hash256(Encoding.UTF8.GetBytes(entity.PartitionKey + entity.RowKey)).ToString();
        }

		public static byte[] Serialize(this ITableEntity entity)
        {
            MemoryStream ms = new MemoryStream();
            using(var messageWriter = new ODataMessageWriter(new Message(ms), new ODataMessageWriterSettings()))
            {
                // Create an entry writer to write a top-level entry to the message.
                ODataWriter entryWriter = messageWriter.CreateODataEntryWriter();
				TableOperationHttpWebRequestFactory.WriteOdataEntity(entity, TableOperationType.Insert, null, entryWriter, null, true);
                return ms.ToArray();
            }
        }

		public static void Deserialize(this ITableEntity entity, byte[] value)
        {
            MemoryStream ms = new MemoryStream(value);
            using(ODataMessageReader messageReader = new ODataMessageReader(new Message(ms), new ODataMessageReaderSettings()
            {
                MessageQuotas = new ODataMessageQuotas()
                {
                    MaxReceivedMessageSize = 20 * 1024 * 1024
                }
            }))
            {
                ODataReader reader = messageReader.CreateODataEntryReader();
                reader.Read();
				TableOperationHttpWebRequestFactory.ReadAndUpdateTableEntity(entity, (ODataEntry)reader.Item, null);
            }
        }

        internal class Message : IODataResponseMessage
        {
            private readonly Stream stream;
            private readonly Dictionary<string, string> headers = new Dictionary<string, string>();

            public Message(Stream stream)
            {
                this.stream = stream;
                SetHeader("Content-Type", "application/atom+xml");
            }

            public string GetHeader(string headerName)
            {
                string value;
                headers.TryGetValue(headerName, out value);
                return value;
            }

            public void SetHeader(string headerName, string headerValue)
            {
                this.headers.Add(headerName, headerValue);
            }

            public Stream GetStream()
            {
                return this.stream;
            }

            public IEnumerable<KeyValuePair<string, string>> Headers
            {
                get
                {
                    return this.headers;
                }
            }

            public int StatusCode
            {
                get;
                set;
            }
        }
    }
}
