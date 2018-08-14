using System;
using System.Text;

namespace Stratis.SmartContracts.ByteHelper
{
    /// <summary>
    /// Provides utilities to transform smart contract primitives into bytes and back.
    /// </summary>
    public static class ByteConverter
    {
        #region bool

        /// <summary>
        /// Convert a bool to a byte.
        /// </summary>
        public static byte ToByte(bool val)
        {
            return val
                ? (byte)1
                : (byte)0;
        }

        /// <summary>
        /// Convert a byte to a bool.
        /// </summary>
        public static bool ToBool(byte val)
        {
            if (val > 0)
                return true;

            return false;
        }

        #endregion

        #region int 

        /// <summary>
        /// Convert an int to a byte array.
        /// </summary>
        public static byte[] ToBytes(int val)
        {
            return BitConverter.GetBytes(val);
        }

        ///<summary>Convert a byte array to an int.</summary>
        ///<remarks>Throws an exception if byte array is less than 4 bytes. If byte array is longer, then first 4 bytes will be used.</remarks>
        public static int ToInt32(byte[] val)
        {
            return BitConverter.ToInt32(val, 0);
        }

        #endregion

        #region uint

        /// <summary>
        /// Convert a uint to a byte array.
        /// </summary>
        public static byte[] ToBytes(uint val)
        {
            return BitConverter.GetBytes(val);
        }

        ///<summary>Convert a byte array to an int.</summary>
        ///<remarks>Throws an exception if byte array is less than 4 bytes. If byte array is longer, then first 4 bytes will be used.</remarks>
        public static uint ToUInt32(byte[] val)
        {
            return BitConverter.ToUInt32(val, 0);
        }

        #endregion

        #region long

        /// <summary>
        /// Convert a long to a byte array.
        /// </summary>
        public static byte[] ToBytes(long val)
        {
            return BitConverter.GetBytes(val);
        }

        ///<summary>Convert a byte array to a long.</summary>
        ///<remarks>Throws an exception if byte array is less than 8 bytes. If byte array is longer, then first 8 bytes will be used.</remarks>
        public static long ToInt64(byte[] val)
        {
            return BitConverter.ToInt64(val, 0);
        }

        #endregion

        #region ulong

        /// <summary>
        /// Convert a ulong to a byte array.
        /// </summary>
        public static byte[] ToBytes(ulong val)
        {
            return BitConverter.GetBytes(val);
        }

        ///<summary>Convert a byte array to a ulong.</summary>
        ///<remarks>Throws an exception if byte array is less than 8 bytes. If byte array is longer, then first 8 bytes will be used.</remarks>
        public static ulong ToUInt64(byte[] val)
        {
            return BitConverter.ToUInt64(val, 0);
        }

        #endregion

        #region string

        /// <summary>
        /// Convert a string to a byte array using UTF8 encoding.
        /// </summary>
        public static byte[] ToBytes(string val)
        {
            return Encoding.UTF8.GetBytes(val);
        }

        /// <summary>
        /// Convert a byte array to a string using UTF8 encoding.
        /// </summary>
        public static string ToString(byte[] val)
        {
            return Encoding.UTF8.GetString(val);
        }

        /// <summary>
        /// Convert a hexadecimal string to a byte array.
        /// </summary>
        public static byte[] FromHex(string val)
        {
            int NumberChars = val.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(val.Substring(i, 2), 16);
            return bytes;
        }

        /// <summary>
        /// Convert a byte array to a hexadecimal string.
        /// </summary>
        public static string ToHex(byte[] val)
        {
            return BitConverter.ToString(val);
        }

        #endregion

        #region Address

        // TODO: Build some infrastructure so we can load Network into this class.

        // That way we can convert addresses to the 20-byte format, as opposed to via the string. 

        #endregion



    }
}
