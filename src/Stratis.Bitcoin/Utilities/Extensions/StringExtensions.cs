using System.Security;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Converts a string to a SecureString object.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        /// <returns>The SecureString result.</returns>
        public static SecureString ToSecureString(this string input)
        {
            var output = new SecureString();
            foreach (char c in input.ToCharArray())
            {
                output.AppendChar(c);
            }

            return output;
        }

        /// <summary>
        /// Retrieves the underlying string from a SecureString object.
        /// </summary>
        /// <param name="secstrPassword">The SecureString object.</param>
        /// <returns>The underlying string contained in this object.</returns>
        public static string FromSecureString(this SecureString secstrPassword)
        {
            return new System.Net.NetworkCredential(string.Empty, secstrPassword).Password;
        }
    }
}
