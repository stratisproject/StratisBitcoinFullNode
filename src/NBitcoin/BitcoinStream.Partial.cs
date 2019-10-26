

using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public partial class BitcoinStream
    {
        private VarInt _VarInt = new VarInt(0);

        private void ReadWriteArray<T>(ref T[] data) where T : IBitcoinSerializable
        {
            if(data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if(this._VarInt.ToLong() > (uint) this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size too big");
            if(!this.Serializing)
                data = new T[this._VarInt.ToLong()];
            for(int i = 0 ; i < data.Length ; i++)
            {
                T obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }


        private void ReadWriteArray(ref ulong[] data)
        {
            if(data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if(this._VarInt.ToLong() > (uint) this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size not big");
            if(!this.Serializing)
                data = new ulong[this._VarInt.ToLong()];
            for(int i = 0 ; i < data.Length ; i++)
            {
                ulong obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }


        private void ReadWriteArray(ref ushort[] data)
        {
            if(data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if(this._VarInt.ToLong() > (uint) this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size not big");
            if(!this.Serializing)
                data = new ushort[this._VarInt.ToLong()];
            for(int i = 0 ; i < data.Length ; i++)
            {
                ushort obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }

        private void ReadWriteArray(ref uint[] data)
        {
            if(data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if(this._VarInt.ToLong() > (uint) this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size not big");
            if(!this.Serializing)
                data = new uint[this._VarInt.ToLong()];
            for(int i = 0 ; i < data.Length ; i++)
            {
                uint obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }

        private void ReadWriteArray(ref long[] data)
        {
            if(data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if(this._VarInt.ToLong() > (uint) this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size not big");
            if(!this.Serializing)
                data = new long[this._VarInt.ToLong()];
            for(int i = 0 ; i < data.Length ; i++)
            {
                long obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }

        private void ReadWriteArray(ref short[] data)
        {
            if(data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if(this._VarInt.ToLong() > (uint) this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size not big");
            if(!this.Serializing)
                data = new short[this._VarInt.ToLong()];
            for(int i = 0 ; i < data.Length ; i++)
            {
                short obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }

        private void ReadWriteArray(ref int[] data)
        {
            if(data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if(this._VarInt.ToLong() > (uint) this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size not big");
            if(!this.Serializing)
                data = new int[this._VarInt.ToLong()];
            for(int i = 0 ; i < data.Length ; i++)
            {
                int obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }

        private void ReadWriteArray(ref string[] data)
        {
            if (data == null && this.Serializing)
                throw new ArgumentNullException("Impossible to serialize a null array");
            this._VarInt.SetValue(data == null ? 0 : (ulong)data.Length);
            ReadWrite(ref this._VarInt);

            if (this._VarInt.ToLong() > (uint)this.MaxArraySize)
                throw new ArgumentOutOfRangeException("Array size not big");

            if (!this.Serializing)
                data = new string[this._VarInt.ToLong()];

            for (int i = 0; i < data.Length; i++)
            {
                string obj = data[i];
                ReadWrite(ref obj);
                data[i] = obj;
            }
        }

        public void ReadWrite(ref ulong[] data)
        {
            ReadWriteArray(ref data);
        }

        public void ReadWrite(ref ushort[] data)
        {
            ReadWriteArray(ref data);
        }

        public void ReadWrite(ref uint[] data)
        {
            ReadWriteArray(ref data);
        }

        public void ReadWrite(ref long[] data)
        {
            ReadWriteArray(ref data);
        }

        public void ReadWrite(ref short[] data)
        {
            ReadWriteArray(ref data);
        }

        public void ReadWrite(ref int[] data)
        {
            ReadWriteArray(ref data);
        }

        public void ReadWrite(ref string[] data)
        {
            ReadWriteArray(ref data);
        }

        private uint256.MutableUint256 _MutableUint256 = new uint256.MutableUint256(uint256.Zero);
        public void ReadWrite(ref uint256 value)
        {
            value = value ?? uint256.Zero;
            this._MutableUint256.Value = value;
            ReadWrite(ref this._MutableUint256);
            value = this._MutableUint256.Value;
        }

        public void ReadWrite(uint256 value)
        {
            value = value ?? uint256.Zero;
            this._MutableUint256.Value = value;
            ReadWrite(ref this._MutableUint256);
            value = this._MutableUint256.Value;
        }

        public void ReadWrite(ref List<uint256> value)
        {
            if(this.Serializing)
            {
                List<uint256.MutableUint256> list = value == null ? null : value.Select(v=>v.AsBitcoinSerializable()).ToList();
                ReadWrite(ref list);
            }
            else
            {
                List<uint256.MutableUint256> list = null;
                ReadWrite(ref list);
                value = list.Select(l=>l.Value).ToList();
            }
        }

        private uint160.MutableUint160 _MutableUint160 = new uint160.MutableUint160(uint160.Zero);
        public void ReadWrite(ref uint160 value)
        {
            value = value ?? uint160.Zero;
            this._MutableUint160.Value = value;
            ReadWrite(ref this._MutableUint160);
            value = this._MutableUint160.Value;
        }

        public void ReadWrite(uint160 value)
        {
            value = value ?? uint160.Zero;
            this._MutableUint160.Value = value;
            ReadWrite(ref this._MutableUint160);
            value = this._MutableUint160.Value;
        }

        public void ReadWrite(ref List<uint160> value)
        {
            if(this.Serializing)
            {
                List<uint160.MutableUint160> list = value == null ? null : value.Select(v=>v.AsBitcoinSerializable()).ToList();
                ReadWrite(ref list);
            }
            else
            {
                List<uint160.MutableUint160> list = null;
                ReadWrite(ref list);
                value = list.Select(l=>l.Value).ToList();
            }
        }


        public void ReadWrite(ref ulong data)
        {
            ulong l = (ulong)data;
            ReadWriteNumber(ref l, sizeof(ulong));
            if(!this.Serializing)
                data = (ulong)l;
        }

        public ulong ReadWrite(ulong data)
        {
            ReadWrite(ref data);
            return data;
        }


        public void ReadWrite(ref ushort data)
        {
            ulong l = (ulong)data;
            ReadWriteNumber(ref l, sizeof(ushort));
            if(!this.Serializing)
                data = (ushort)l;
        }

        public ushort ReadWrite(ushort data)
        {
            ReadWrite(ref data);
            return data;
        }


        public void ReadWrite(ref uint data)
        {
            ulong l = (ulong)data;
            ReadWriteNumber(ref l, sizeof(uint));
            if(!this.Serializing)
                data = (uint)l;
        }

        public uint ReadWrite(uint data)
        {
            ReadWrite(ref data);
            return data;
        }




        public void ReadWrite(ref long data)
        {
            long l = (long)data;
            ReadWriteNumber(ref l, sizeof(long));
            if(!this.Serializing)
                data = (long)l;
        }

        public long ReadWrite(long data)
        {
            ReadWrite(ref data);
            return data;
        }


        public void ReadWrite(ref short data)
        {
            long l = (long)data;
            ReadWriteNumber(ref l, sizeof(short));
            if(!this.Serializing)
                data = (short)l;
        }

        public short ReadWrite(short data)
        {
            ReadWrite(ref data);
            return data;
        }


        public void ReadWrite(ref int data)
        {
            long l = (long)data;
            ReadWriteNumber(ref l, sizeof(int));
            if(!this.Serializing)
                data = (int)l;
        }

        public int ReadWrite(int data)
        {
            ReadWrite(ref data);
            return data;
        }

            }
}