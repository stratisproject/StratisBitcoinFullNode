using System;
using DBreeze;

namespace Stratis.DB
{
    public interface IStratisDB : IDisposable
    {
        IStratisDBTransaction CreateTransaction(StratisDBTransactionMode mode, params string[] tables);
    }

    public class StratisDB : IStratisDB
    {
        internal DBreezeEngine DBreezeEngine;

        /// <summary>The serializer to use for this transaction.</summary>
        public IStratisDBSerializer StratisSerializer { get; private set; }

        /// <summary>Interface providing control over the updating of transient lookups.</summary>
        public IStratisDBTrackers Lookups { get; private set; }

        public StratisDB(
            string dataFolderName,
            IStratisDBSerializer stratisSerializer,
            IStratisDBTrackers lookups = null)
        {
            this.DBreezeEngine = new DBreezeEngine(dataFolderName);
            this.StratisSerializer = stratisSerializer;
            this.Lookups = lookups;
        }

        public IStratisDBTransaction CreateTransaction(StratisDBTransactionMode mode, params string[] tables)
        {
            return new StratisDBTransaction(this, mode, tables);
        }

        public void Dispose()
        {
            this.DBreezeEngine.Dispose();
        }
    }
}
