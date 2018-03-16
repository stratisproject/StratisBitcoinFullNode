using System;

namespace Stratis.SmartContracts.Tools.Validation
{
    // Borrowed from Stratis.SmartContracts.Util
    public static class Extensions
    {
        public static string ToHexString(this byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }        
    }
}
