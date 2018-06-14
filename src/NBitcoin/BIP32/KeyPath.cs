using System;
using System.Globalization;
using System.Linq;

namespace NBitcoin
{

    /// <summary>
    /// Represent a path in the hierarchy of HD keys (BIP32)
    /// </summary>
    public class KeyPath
    {
        public KeyPath()
        {
            this._Indexes = new uint[0];
        }

        /// <summary>
        /// Parse a KeyPath
        /// </summary>
        /// <param name="path">The KeyPath formated like 10/0/2'/3</param>
        /// <returns></returns>
        public static KeyPath Parse(string path)
        {
            uint[] parts = path
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "m")
                .Select(ParseCore)
                .ToArray();
            return new KeyPath(parts);
        }

        public KeyPath(string path)
        {
            this._Indexes =
                path
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "m")
                .Select(ParseCore)
                .ToArray();
        }

        private static uint ParseCore(string i)
        {
            bool hardened = i.EndsWith("'");
            string nonhardened = hardened ? i.Substring(0, i.Length - 1) : i;
            uint index = uint.Parse(nonhardened);
            return hardened ? index | 0x80000000u : index;
        }

        public KeyPath(params uint[] indexes)
        {
            this._Indexes = indexes;
        }

        private readonly uint[] _Indexes;
        public uint this[int index]
        {
            get
            {
                return this._Indexes[index];
            }
        }

        public uint[] Indexes
        {
            get
            {
                return this._Indexes.ToArray();
            }
        }

        public KeyPath Derive(int index, bool hardened)
        {
            if(index < 0)
                throw new ArgumentOutOfRangeException("index", "the index can't be negative");
            uint realIndex = (uint)index;
            realIndex = hardened ? realIndex | 0x80000000u : realIndex;
            return Derive(new KeyPath(realIndex));
        }

        public KeyPath Derive(uint index)
        {
            return Derive(new KeyPath(index));
        }

        public KeyPath Derive(KeyPath derivation)
        {
            return new KeyPath(this._Indexes
                .Concat(derivation._Indexes)
                .ToArray());
        }

        public KeyPath Parent
        {
            get
            {
                if(this._Indexes.Length == 0)
                    return null;
                return new KeyPath(this._Indexes.Take(this._Indexes.Length - 1).ToArray());
            }
        }

        public KeyPath Increment()
        {
            if(this._Indexes.Length == 0)
                return null;
            uint[] indices = this._Indexes.ToArray();
            indices[indices.Length - 1]++;
            return new KeyPath(indices);
        }

        public override bool Equals(object obj)
        {
            var item = obj as KeyPath;
            if(item == null)
                return false;
            return ToString().Equals(item.ToString());
        }
        public static bool operator ==(KeyPath a, KeyPath b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return a.ToString() == b.ToString();
        }

        public static bool operator !=(KeyPath a, KeyPath b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        private string _Path;
        public override string ToString()
        {
            return this._Path ?? (this._Path = string.Join("/", this._Indexes.Select(ToString).ToArray()));
        }

        private static string ToString(uint i)
        {
            bool hardened = (i & 0x80000000u) != 0;
            uint nonhardened = (i & ~0x80000000u);
            return hardened ? nonhardened + "'" : nonhardened.ToString(CultureInfo.InvariantCulture);
        }

        public bool IsHardened
        {
            get
            {
                if(this._Indexes.Length == 0)
                    throw new InvalidOperationException("No indice found in this KeyPath");
                return (this._Indexes[this._Indexes.Length - 1] & 0x80000000u) != 0;
            }
        }
    }
}
