using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal static class LinqExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            T[] bucket = null;
            var count = 0;

            foreach (var item in source)
            {
                if (bucket == null)
                    bucket = new T[size];

                bucket[count++] = item;

                if (count != size)
                    continue;

                yield return bucket.Select(x => x);

                bucket = null;
                count = 0;
            }

            // Return the last bucket with all remaining elements
            if (bucket != null && count > 0)
                yield return bucket.Take(count);
        }
    }

    internal class TempRow
    {
        private PropertyInfo[] GetProperties()
        {
            return this.GetType().GetProperties().Where(p => p.SetMethod != null).ToArray();
        }

        public override string ToString()
        {
            return string.Join(", ", this.GetProperties().Select(p => $"{p.Name}={DBParameter.Create(p.GetValue(this))}"));
        }
    }

    internal class TempTable : List<TempRow>
    {
        internal TempTable(Type rowType)
        {
            this.RowType = rowType;
        }

        internal static TempTable Create<T>() where T : TempRow
        {
            return new TempTable(typeof(T));
        }

        internal Type RowType { get; set; }

        private static PropertyInfo[] GetProperties(Type objType)
        {
            return objType.GetProperties().Where(p => p.SetMethod != null).ToArray();
        }

        private static string ColumnType(PropertyInfo info)
        {
            string type = "TEXT";

            if (info.PropertyType == typeof(int) || info.PropertyType == typeof(long))
                return "INT";

            return type;
        }

        protected string ObjectColumns(bool includeType = false)
        {
            var props = GetProperties(this.RowType);

            if (!includeType)
                return $"({string.Join(",", props.Select(info => info.Name))})";

            return $"({string.Join(",", props.Select(info => $"{info.Name} {ColumnType(info)}"))})";
        }

        internal IEnumerable<string> CreateScript()
        {
            yield return $"CREATE TABLE IF NOT EXISTS temp.{this.RowType.Name} {ObjectColumns(true)};";
            yield return $"DELETE FROM temp.{this.RowType.Name}";

            var props = GetProperties(this.RowType);

            foreach (IEnumerable<TempRow> batch in ((IEnumerable<TempRow>)this).Batch(500))
                yield return $"INSERT INTO temp.{this.RowType.Name} {ObjectColumns()} VALUES {string.Join(Environment.NewLine + ",", batch.Select(obj => ObjectRow(props, obj)))};";
        }

        internal static string ObjectRow(PropertyInfo[] props, object obj)
        {
            var res = props.Select(p => p.GetValue(obj)).Select(prop => DBParameter.Create(prop));
            var arr = string.Join(",", res);
            return $"({arr})";
        }
    }
}