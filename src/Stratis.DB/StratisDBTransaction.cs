using System;
using System.Collections.Generic;
using System.Linq;
using DBreeze.DataTypes;
using DBreeze.Utils;
using NBitcoin;

namespace Stratis.DB
{
    /// <summary>Supported DBreeze transaction modes.</summary>
    public enum StratisDBTransactionMode
    {
        Read,
        ReadWrite
    }

    public interface IStratisDBTransaction : IDisposable
    {
        void Insert<TKey, TObject>(string tableName, TKey key, TObject obj);
        void InsertMultiple<TKey, TObject>(string tableName, (TKey, TObject)[] objects);
        void InsertDictionary<TKey, TObject>(string tableName, Dictionary<TKey, TObject> objects);
        bool Select<TKey, TObject>(string tableName, TKey key, out TObject obj);
        List<TObject> SelectMultiple<TKey, TObject>(string tableName, TKey[] keys);
        Dictionary<TKey, TObject> SelectDictionary<TKey, TObject>(string tableName);
        IEnumerable<(TKey, TObject)> SelectForward<TKey, TObject>(string tableName);
        void RemoveKey<TKey, TObject>(string tableName, TKey key, TObject obj);
        void RemoveAllKeys(string tableName);
        bool Exists<TKey>(string tableName, TKey key);
        bool[] ExistsMultiple<TKey>(string tableName, TKey[] keys);
        void Commit();
        void Rollback();
        string ToString();
    }

    /// <summary>
    /// The class provides a layer of abstraction to the underlying DBreeze transaction.
    /// It also provides a mechanism to keep transient lookups (if any) in sync with changes
    /// to the database. It also handles all required serialization here in one place.
    /// </summary>
    public class StratisDBTransaction : IStratisDBTransaction
    {
        /// <summary>The serializer to use for this transaction.</summary>
        private readonly IStratisDBSerializer stratisSerializer;

        /// <summary>The underlying DBreeze transaction.</summary>
        private DBreeze.Transactions.Transaction transaction;

        /// <summary>Interface providing control over the updating of transient lookups.</summary>
        private readonly IStratisDBTrackers lookups;

        /// <summary>The mode of the transaction.</summary>
        private readonly StratisDBTransactionMode mode;

        /// <summary>Tracking changes allows updating of transient lookups after a successful commit operation.</summary>
        private Dictionary<string, IStratisDBTracker> trackers;

        /// <summary>
        /// Constructs a transaction object that acts as a wrapper around the database tables.
        /// </summary>
        /// <param name="stratisDB">The database engine.</param>
        /// <param name="mode">The mode in which to interact with the database.</param>
        /// <param name="tables">The tables being updated if any.</param>
        internal StratisDBTransaction(
            StratisDB stratisDB,
            StratisDBTransactionMode mode,
            params string[] tables)
        {
            this.transaction = stratisDB.DBreezeEngine.GetTransaction();
            this.mode = mode;

            this.transaction.ValuesLazyLoadingIsOn = false;

            if ((mode == StratisDBTransactionMode.ReadWrite) && (tables.Length != 0))
            {
                this.transaction.SynchronizeTables(tables);
            }

            this.stratisSerializer = stratisDB.StratisSerializer;
            this.lookups = stratisDB.Lookups;
            this.trackers = stratisDB.Lookups?.CreateTrackers(tables);
        }

        public void Insert<TKey, TObject>(string tableName, TKey key, TObject obj)
        {
            AssertWritable();

            byte[] keyBytes = this.stratisSerializer.Serialize(key);

            if (TypeRequiresSerializer(typeof(TObject)))
            {
                byte[] objBytes = this.stratisSerializer.Serialize(obj);
                this.transaction.Insert(tableName, keyBytes, objBytes);
            }
            else
            {
                this.transaction.Insert(tableName, keyBytes, obj);
            }

            // If this is a tracked class.
            if (this.trackers != null && this.trackers.TryGetValue(tableName, out IStratisDBTracker tracker))
            {
                // Record the object and its value.
                tracker.ObjectEvent(obj, StratisDBEvent.ObjectCreated);
            }
        }

        public void InsertMultiple<TKey, TObject>(string tableName, (TKey, TObject)[] objects)
        {
            foreach ((TKey, TObject) kv in objects.OrderBy(x => this.stratisSerializer.Serialize(x.Item1), new ByteListComparer()))
            {
                this.Insert(tableName, kv.Item1, kv.Item2);
            }
        }

        public void InsertDictionary<TKey, TObject>(string tableName, Dictionary<TKey, TObject> objects)
        {
            this.InsertMultiple(tableName, objects.Select(o => (o.Key, o.Value)).ToArray());
        }

        public bool Exists<TKey>(string tableName, TKey key)
        {
            bool saveLazyLoading = this.transaction.ValuesLazyLoadingIsOn;
            this.transaction.ValuesLazyLoadingIsOn = true;
            bool res = this.transaction.Select<byte[], byte[]>(tableName, this.stratisSerializer.Serialize(key)).Exists;
            this.transaction.ValuesLazyLoadingIsOn = saveLazyLoading;
            return res;
        }

        public bool[] ExistsMultiple<TKey>(string tableName, TKey[] keys)
        {
            bool saveLazyLoading = this.transaction.ValuesLazyLoadingIsOn;
            this.transaction.ValuesLazyLoadingIsOn = true;
            List<byte[]> serializedKeys = keys.Select(k => this.stratisSerializer.Serialize(k)).ToList();

            Dictionary<byte[], bool> exists = serializedKeys.ToDictionary(k => k, k => false);

            foreach (KeyValuePair<byte[], bool> kv in exists.OrderBy(x => x.Key, new ByteListComparer()))
            {
                Row<byte[], byte[]> row = this.transaction.Select<byte[], byte[]>(tableName, kv.Key);
                exists[kv.Key] = row.Exists;
            }

            bool[] res = serializedKeys.Select(k => exists[k]).ToArray();

            this.transaction.ValuesLazyLoadingIsOn = saveLazyLoading;

            return res;
        }

        private bool TypeRequiresSerializer(Type type)
        {
            return !type.IsPrimitive;
        }

        public bool Select<TKey, TObject>(string tableName, TKey key, out TObject obj)
        {
            byte[] keyBytes = this.stratisSerializer.Serialize(key);

            if (TypeRequiresSerializer(typeof(TObject)))
            {
                Row<byte[], byte[]> row = this.transaction.Select<byte[], byte[]>(tableName, keyBytes);

                if (!row.Exists)
                {
                    obj = default(TObject);
                    return false;
                }

                obj = (TObject)this.stratisSerializer.Deserialize(row.Value, typeof(TObject));
            }
            else
            {
                Row<byte[], TObject> row = this.transaction.Select<byte[], TObject>(tableName, keyBytes);

                if (!row.Exists)
                {
                    obj = default(TObject);
                    return false;
                }

                obj = row.Value;
            }

            // If this is a tracked table.
            if (this.trackers != null && this.trackers.TryGetValue(tableName, out IStratisDBTracker tracker))
            {
                // Set the old value on the object itself so that we can update the lookups if it is changed.
                tracker.ObjectEvent(obj, StratisDBEvent.ObjectRead);
            }

            return true;
        }

        public List<TObject> SelectMultiple<TKey, TObject>(string tableName, TKey[] keys)
        {
            List<byte[]> serializedKeys = keys.Select(k => this.stratisSerializer.Serialize(k)).ToList();

            Dictionary<byte[], TObject> objects = serializedKeys.ToDictionary(k => k, k => default(TObject));

            foreach (KeyValuePair<byte[], TObject> kv in objects.OrderBy(x => x.Key, new ByteListComparer()))
            {
                if (this.Select(tableName, kv.Key, out TObject obj))
                {
                    objects[kv.Key] = obj;
                }
            }

            return serializedKeys.Select(k => objects[k]).ToList();
        }

        public Dictionary<TKey, TObject> SelectDictionary<TKey, TObject>(string tableName)
        {
            var res = new Dictionary<TKey, TObject>();

            foreach ((TKey, TObject) kv in SelectForward<TKey, TObject>(tableName))
            {
                res[kv.Item1] = kv.Item2;
            }

            return res;
        }

        public IEnumerable<(TKey, TObject)> SelectForward<TKey, TObject>(string tableName)
        {
            if (this.trackers == null || !this.trackers.TryGetValue(tableName, out IStratisDBTracker tracker))
                tracker = null;

            IEnumerable<(TKey, TObject)> objects;

            if (TypeRequiresSerializer(typeof(TObject)))
            {
                objects = this.transaction.SelectForward<byte[], byte[]>(tableName).Select(r => (
                    (TKey)this.stratisSerializer.Deserialize(r.Key, typeof(TKey)),
                    (TObject)this.stratisSerializer.Deserialize(r.Value, typeof(TObject))));
            }
            else
            {
                objects = this.transaction.SelectForward<byte[], TObject>(tableName).Select(r => (
                    (TKey)this.stratisSerializer.Deserialize(r.Key, typeof(TKey)),
                    r.Value));
            }

            foreach ((TKey, TObject) obj in objects)
            {
                // If this is a tracked table.
                if (tracker != null)
                {
                    // Set the old value on the object itself so that we can update the lookups if it is changed.
                    tracker.ObjectEvent(obj.Item2, StratisDBEvent.ObjectRead);
                }

                yield return obj;
            }
        }

        public void RemoveKey<TKey, TObject>(string tableName, TKey key, TObject obj)
        {
            AssertWritable();

            byte[] keyBytes = this.stratisSerializer.Serialize(key);
            this.transaction.RemoveKey(tableName, keyBytes);

            // If this is a tracked table.
            if (this.trackers != null && this.trackers.TryGetValue(tableName, out IStratisDBTracker tracker))
            {
                // Record the object and its old value.
                tracker.ObjectEvent(obj, StratisDBEvent.ObjectDeleted);
            }
        }

        public void RemoveAllKeys(string tableName)
        {
            this.transaction.RemoveAllKeys(tableName, true);
        }

        public void Commit()
        {
            AssertWritable();

            this.transaction.Commit();

            // Having trackers allows us to postpone updating the lookups
            // until after a successful commit.
            this.lookups?.OnCommit(this.trackers);
        }

        public void Rollback()
        {
            AssertWritable();

            this.transaction.Rollback();
        }

        private void AssertWritable()
        {
            if (this.mode != StratisDBTransactionMode.ReadWrite)
                throw new InvalidOperationException("The transaction does not allow write operations.");
        }

        /// <summary>A string to identify this transaction by.</summary>
        /// <returns>A concatenation of the creation time and thread id.</returns>
        public override string ToString()
        {
            DateTime createdDT = new DateTime(this.transaction.CreatedUdt);
            return string.Format("{0}:{1}", createdDT, this.transaction.ManagedThreadId);
        }

        public void Dispose()
        {
            if (this.transaction != null)
            {
                this.transaction.Dispose();
                this.transaction = null;
            }
        }
    }
}