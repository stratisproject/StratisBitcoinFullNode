using System;
using System.Globalization;
using System.Collections;

namespace NBitcoin.BouncyCastle.Utilities
{
    internal abstract class Platform
    {
        private static readonly CompareInfo InvariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

        private static string GetNewLine()
        {
            return Environment.NewLine;
        }

        internal static bool EqualsIgnoreCase(string a, string b)
        {
            return ToUpperInvariant(a) == ToUpperInvariant(b);
        }

        internal static string GetEnvironmentVariable(
            string variable)
        {
            try
            {
                return Environment.GetEnvironmentVariable(variable);
            }
            catch (System.Security.SecurityException)
            {
                // We don't have the required permission to read this environment variable,
                // which is fine, just act as if it's not set
                return null;
            }
        }

        internal static Exception CreateNotImplementedException(
            string message)
        {
            return new NotImplementedException(message);
        }

        internal static IList CreateArrayList()
        {
            return new ArrayList();
        }
        internal static IList CreateArrayList(int capacity)
        {
            return new ArrayList(capacity);
        }
        internal static IList CreateArrayList(ICollection collection)
        {
            return new ArrayList(collection);
        }
        internal static IList CreateArrayList(IEnumerable collection)
        {
            var result = new ArrayList();
            foreach (object o in collection)
            {
                result.Add(o);
            }
            return result;
        }
        internal static IDictionary CreateHashtable()
        {
            return new Hashtable();
        }
        internal static IDictionary CreateHashtable(int capacity)
        {
            return new Hashtable(capacity);
        }
        internal static IDictionary CreateHashtable(IDictionary dictionary)
        {
            return new Hashtable(dictionary);
        }

        internal static string ToLowerInvariant(string s)
        {
#if NETCORE
            return s.ToLowerInvariant();
#else
            return s.ToLower(CultureInfo.InvariantCulture);
#endif
        }

        internal static string ToUpperInvariant(string s)
        {
#if NETCORE
            return s.ToUpperInvariant();
#else
            return s.ToUpper(CultureInfo.InvariantCulture);
#endif
        }

        internal static readonly string NewLine = GetNewLine();

#if NETCORE
        internal static void Dispose(IDisposable d)
        {
            d.Dispose();
        }
#else
        internal static void Dispose(Stream s)
        {
            s.Close();
        }
        internal static void Dispose(TextWriter t)
        {
            t.Close();
        }
#endif

        internal static int IndexOf(string source, string value)
        {
            return InvariantCompareInfo.IndexOf(source, value, CompareOptions.Ordinal);
        }

        internal static int LastIndexOf(string source, string value)
        {
            return InvariantCompareInfo.LastIndexOf(source, value, CompareOptions.Ordinal);
        }

        internal static bool StartsWith(string source, string prefix)
        {
            return InvariantCompareInfo.IsPrefix(source, prefix, CompareOptions.Ordinal);
        }

        internal static bool EndsWith(string source, string suffix)
        {
            return InvariantCompareInfo.IsSuffix(source, suffix, CompareOptions.Ordinal);
        }

        internal static string GetTypeName(object obj)
        {
            return obj.GetType().FullName;
        }
    }
}
