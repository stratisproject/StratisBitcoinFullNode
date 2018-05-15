using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    public static class StringExtensions
    {
        public static SecureString ToSecureString(this string input)
        {
            var output = new SecureString();
            foreach (char c in input.ToCharArray())
            {
                output.AppendChar(c);
            }

            return output;
        }

        public static string FromSecureString(this SecureString input)
        {
            return new System.Net.NetworkCredential(string.Empty, input).Password;
        }
    }
}
