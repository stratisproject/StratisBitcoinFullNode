using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using DBreeze.Utils;
using NBitcoin;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexExpression
    {
        public static string[] DefaultDependancies = new string[] {
                    "System", "System.Linq", "System.Linq.Expressions", "System.Collections.Generic", "NBitcoin" };

        [JsonProperty(PropertyName = "Builder")]
        public string Builder { get; private set; }
        [JsonProperty(PropertyName = "Many")]
        public bool Many { get; private set; }
        [JsonProperty(PropertyName = "Uses")]
        public string[] Dependencies { get; private set; }
        [JsonIgnore()]
        public Expression<Func<Transaction, Block, Network, IEnumerable<object[]>>> expression { get; private set; }
        [JsonIgnore()]
        public Func<Transaction, Block, Network, IEnumerable<object[]>> compiled = null;

        public IndexExpression(bool multiValue, string builder, string[] dependencies = null)
        {
            if (dependencies == null)
                dependencies = DefaultDependancies;
            this.Builder = builder;
            this.Dependencies = dependencies;
            this.Many = multiValue;
        }

        public void Compile()
        {
            if (this.Builder != null && this.compiled == null)
            {
                var classSource = string.Join(Environment.NewLine, this.Dependencies.Select(dependancy => $"using {dependancy};").ToArray()) + Environment.NewLine +
                    Environment.NewLine + "Expression<Func<Transaction, Block, Network, IEnumerable<object[]>>> builder = " + this.Builder + ";";
                var options = ScriptOptions.Default.AddReferences(this.Dependencies);
                ScriptState state = CSharpScript.RunAsync(classSource, options).GetAwaiter().GetResult();
                this.expression = state.GetVariable("builder").Value as Expression<Func<Transaction, Block, Network, IEnumerable<object[]>>>;
                this.compiled = this.expression.Compile();
            }
        }

        public bool Equals(IndexExpression other)
        {
            return this.Builder == other.Builder;
        }
    }

    public class Index:IndexExpression
    {     
        public class Comparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] a, byte[] b)
            {
                if (a.Length != b.Length)
                    return false;

                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i])
                        return false;

                return true;
            }

            public int GetHashCode(byte[] key)
            {
                int hash = 17;
                for (int i = 0; i < key.Length; i++)
                    hash += hash * 31 + key[i];
                return hash;
            }
        }

        [JsonProperty(PropertyName = "Name")]
        public string Name { get; private set; }
        [JsonProperty(PropertyName = "Table")]
        public string Table { get; private set; }

        private IndexRepository repository;
        private readonly IndexSession session;
        private Comparer comparer = new Comparer();
        private ByteListComparer byteListComparer = new ByteListComparer();

        public Index(IndexRepository repository) 
            :base(false, null)
        {
            this.repository = repository;
            this.session = repository.GetSession();
        }

        public Index(IndexRepository repository, string name, bool multiValue, string builder, string[] dependencies = null)
            :base(multiValue, builder, dependencies)
        {
            this.repository = repository;
            this.session = repository.GetSession();
            this.Name = name;
            this.Table = repository.IndexTableName(name);

            try
            {
                this.Compile();
            }
            catch (Exception e)
            {
                throw new IndexStoreException("Could not compile index '" + name + "': " + e.Message);
            }
        }
        
        public static Index Parse(IndexRepository repository, string json, string table = null)
        {
            var index = new Index(repository);

            JsonConvert.PopulateObject(json, index);
            if (table != null)
                index.Table = table;

            try
            {
                index.Compile();
            }
            catch (Exception e)
            {
                throw new IndexStoreException("Could not compile index '" + index.Name + "': " + e.Message);
            }

            return index;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public delegate void recordIndexed(object sender, byte[] key, byte[] value);

        public event recordIndexed recordAdded;
        public event recordIndexed recordRemoved;

        public IEnumerable<byte[]> LookupMultiple(List<byte[]> keyList)
        {
            if (this.Many)
                throw new IndexStoreException("Index '" + this.Name + "' can't be multi-value");

            foreach (var key in keyList)
            {
                if (key != null)
                {
                    var trxId = this.session.Transaction.Select<byte[], byte[]>(this.Table, key);

                    if (trxId.Exists)
                    {
                        yield return trxId.Value;
                        continue;
                    }
                }

                yield return null;
            }
        }

        public IEnumerable<byte[]> EnumerateValues(byte[] key)
        {
            if (!this.Many)
                throw new IndexStoreException("Index '" + this.Name + "' should be multi-value");

            var addrRow = this.session.Transaction.Select<byte[], byte[]>(this.Table, key);
            if (addrRow.Exists)
            {
                // Get the current blob size
                var sizeBytes = addrRow.GetValuePart(0, sizeof(uint));
                if (BitConverter.IsLittleEndian) sizeBytes = sizeBytes.Reverse();
                uint size = BitConverter.ToUInt32(sizeBytes, 0);

                for (uint offset = 0; offset < size;)
                {
                    var itemSizeBytes = addrRow.GetValuePart(offset, sizeof(uint));
                    var itemSize = BitConverter.ToUInt32(itemSizeBytes, 0);
                    yield return addrRow.GetValuePart(offset + 2, itemSize);
                    offset += (itemSize + 2);
                }
            }
        }

        public int IndexTransactionDetails(List<(Transaction, Block)> transactions, bool remove = false)
        {
            if (this.Many)
                return IndexTransactionDetailsMany(transactions, remove);
            else
                return IndexTransactionDetailsOne(transactions, remove);
        }

        private int IndexTransactionDetailsOne(List<(Transaction, Block)> transactions, bool remove = false)
        {
            var itemDict = new Dictionary<byte[], byte[]>(this.comparer);

            foreach (var (transaction, block) in transactions)
            {
                var blockId = block.GetHash();
                var trxId = transaction.GetHash();
                
                foreach (object[] kv in this.compiled(transaction, block, this.repository.GetNetwork()))
                {
                    var key = kv[0].ToBytes();
                    var value = kv[1].ToBytes();
                    itemDict[key] = value;
                }
            }

            // Sort items. Be consistent in always converting our keys to byte arrays using the ToBytes method.
            var itemList = itemDict.ToList();
            itemList.Sort((pair1, pair2) => this.byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

            int count = 0;

            // Index scripts
            foreach (KeyValuePair<byte[], byte[]> kv in itemList)
            {
                count++;
                if (remove)
                {
                    this.session.Transaction.RemoveKey<byte[]>(this.Table, kv.Key);
                    if (recordRemoved != null)
                        recordRemoved(this, kv.Key, kv.Value);
                }
                else
                {
                    this.session.Transaction.Insert<byte[], byte[]>(this.Table, kv.Key, kv.Value);
                    if (recordAdded != null)
                        recordAdded(this, kv.Key, kv.Value);
                }
            }

            return count;
        }

        private int IndexTransactionDetailsMany(List<(Transaction, Block)> transactions, bool remove = false)
        {
            var itemDict = new Dictionary<byte[], HashSet<byte[]>>(this.comparer);

            foreach (var (transaction, block) in transactions)
            {
                var blockId = block.GetHash();
                var trxId = transaction.GetHash();

                foreach (object[] kv in this.compiled(transaction, block, this.repository.GetNetwork()))
                {
                    var key = kv[0].ToBytes();
                    var value = kv[1].ToBytes();

                    HashSet<byte[]> set = itemDict.TryGet(key);
                    if (set == null)
                    {
                        set = new HashSet<byte[]>(this.comparer);
                        itemDict[key] = set;
                    }
                    set.Add(value);
                }
            }

            // Sort items. Be consistent in always converting our keys to byte arrays using the ToBytes method.
            var itemList = itemDict.ToList();
            itemList.Sort((pair1, pair2) => this.byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

            int count = 0;

            // Index scripts
            foreach (KeyValuePair<byte[], HashSet<byte[]>> kv in itemList)
            {
                count++;
                if (remove)
                {
                    this.DeleteValues(kv.Key, kv.Value);

                    if (recordRemoved != null)
                        foreach (var value in kv.Value)
                            recordRemoved(this, kv.Key, value);

                }
                else
                {
                    this.InsertValues(kv.Key, kv.Value);

                    if (recordAdded != null)
                        foreach (var value in kv.Value)
                            recordAdded(this, kv.Key, value);
                }
            }

            return count;
        }

        // Append values to bag of values
        private int InsertValues(byte[] key, HashSet<byte[]> values)
        {
            if (values.Count == 0)
                return 0;

            uint size = 0;
            var addrRow = this.session.Transaction.Select<byte[], byte[]>(this.Table, key);

            if (addrRow.Exists)
            {
                // Get the current size of the stored blob
                var bytes = addrRow.GetValuePart(0, sizeof(uint));
                if (BitConverter.IsLittleEndian) bytes = bytes.Reverse();
                size = BitConverter.ToUInt32(bytes, 0);
            }

            uint appendSize = 0;
            foreach (var value in values)
                appendSize += (2 + (uint)value.Length);

            // Force the value's size to a power of 2 that will fit all the additional values
            if (addrRow.Value == null || ((4 + size + appendSize) > addrRow.Value.Length))
            {
                uint growSize = 1;
                for (; growSize < (4 + size + appendSize); growSize *= 2) { }
                this.session.Transaction.InsertPart<byte[], byte[]>(this.Table, key, new byte[] { 0 /* Dummy */}, growSize - 1);
            }

            var appendBuffer = new byte[appendSize];
            uint appendPos = 4 + size;
            foreach (var value in values)
            {
                byte[] valueBytes = ((ushort)value.Length).ToBytes().Concat(value.ToBytes());
                this.session.Transaction.InsertPart<byte[], byte[]>(this.Table, key, valueBytes, appendPos);
                appendPos += (uint)valueBytes.Length;
            }

            byte[] sizeBytes = (size + appendSize).ToBytes();

            this.session.Transaction.InsertPart<byte[], byte[]>(this.Table, key, sizeBytes, 0);

            return values.Count;
        }

        // Remove values from bag of values
        private int DeleteValues(byte[] key, HashSet<byte[]> list)
        {
            if (list.Count == 0)
                return 0;

            var addrRow = this.session.Transaction.Select<byte[], byte[]>(this.Table, key);
            if (!addrRow.Exists)
                return 0;

            // Get the current blob size
            var sizeBytes = addrRow.GetValuePart(0, sizeof(uint));
            if (BitConverter.IsLittleEndian) sizeBytes = sizeBytes.Reverse();
            uint size = BitConverter.ToUInt32(sizeBytes, 0);

            // Sort the list of items to remove for efficiency
            var sortedList = list.ToList();
            sortedList.Sort((item1, item2) => this.byteListComparer.Compare(item1, item2));
            byte[][] sortedArray = sortedList.ToArray();

            int removeCnt = 0;
            uint offset = 0;
            uint keepOffset = 0;
            uint rebuildOffset = 0;
            uint size2;
            var value = addrRow.GetValuePart(0, size);

            void FlushRebuild()
            {
                if (keepOffset < offset)
                {
                    var keepBytes = (new ArraySegment<byte>(value, (int)keepOffset, (int)(offset - keepOffset))).ToBytes();
                    this.session.Transaction.InsertPart<byte[], byte[]>(this.Table, key, keepBytes, rebuildOffset);
                    rebuildOffset += (uint)keepBytes.Length;
                }
            }

            for (; offset < size; offset += size2)
            {
                size2 = BitConverter.ToUInt32(value, (int)offset);

                // Compare to all values
                int i = 0;
                int j = 0;
                for (; j < size2; j++)
                {
                    for (; i < sortedArray.Length && (j >= sortedArray[i].Length || sortedArray[i][j] < value[offset]); i++) { }
                    if (i >= sortedArray.Length || j >= sortedArray[i].Length || sortedArray[i][j] != value[offset])
                        break;
                }

                if (j == size2) // Match
                {
                    removeCnt++;
                    FlushRebuild();
                    keepOffset = offset + size2;
                }
            }

            if (removeCnt > 0)
            {
                FlushRebuild();
                this.session.Transaction.InsertPart<byte[], byte[]>(this.Table, key, rebuildOffset.ToBytes(), 0);
            }

            return removeCnt;
        }
    }
}
