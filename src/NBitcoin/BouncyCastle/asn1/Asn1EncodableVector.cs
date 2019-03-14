using System;
using System.Collections;
using Stratis.Bitcoin.NBitcoin.BouncyCastle.Utilities;

namespace Stratis.Bitcoin.NBitcoin.BouncyCastle.Asn1
{
    internal class Asn1EncodableVector
        : IEnumerable
    {
        private IList v = Platform.CreateArrayList();

        public static Asn1EncodableVector FromEnumerable(
            IEnumerable e)
        {
            var v = new Asn1EncodableVector();
            foreach(Asn1Encodable obj in e)
            {
                v.Add(obj);
            }
            return v;
        }

        //        public Asn1EncodableVector()
        //        {
        //        }

        public Asn1EncodableVector(
            params Asn1Encodable[] v)
        {
            Add(v);
        }

        //        public void Add(
        //            Asn1Encodable obj)
        //        {
        //            v.Add(obj);
        //        }

        public void Add(
            params Asn1Encodable[] objs)
        {
            foreach(Asn1Encodable obj in objs)
            {
                this.v.Add(obj);
            }
        }

        public void AddOptional(
            params Asn1Encodable[] objs)
        {
            if(objs != null)
            {
                foreach(Asn1Encodable obj in objs)
                {
                    if(obj != null)
                    {
                        this.v.Add(obj);
                    }
                }
            }
        }

        public Asn1Encodable this[
            int index]
        {
            get
            {
                return (Asn1Encodable) this.v[index];
            }
        }

        [Obsolete("Use 'object[index]' syntax instead")]
        public Asn1Encodable Get(
            int index)
        {
            return this[index];
        }

        [Obsolete("Use 'Count' property instead")]
        public int Size
        {
            get
            {
                return this.v.Count;
            }
        }

        public int Count
        {
            get
            {
                return this.v.Count;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return this.v.GetEnumerator();
        }
    }
}
