using System;

namespace Stratis.SmartContracts.CLR
{
    public static class ArrayExtensions
    {
        public static T[] Slice<T>(this T[] arr, uint start, uint length)
        {
            if (start + length > arr.Length)
            {
                throw new ArgumentOutOfRangeException("Array is not long enough");
            }

            var result = new T[length];
            Array.Copy(arr, start, result, 0, length);

            return result;
        }
    }
}