using System.Diagnostics;
using System.Linq;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    /// <summary>
    /// Extension methods for command line arguments.
    /// </summary>
    [DebuggerStepThrough]
    public static class CommandLineArgsExtensions
    {
        /// <summary>
        /// Obtains a value of command line argument.
        /// <para>
        /// It is expected that arguments are written on command line as <c>argName=argValue</c>,
        /// where argName usually (but does not need to) starts with "-".
        /// </para>
        /// <para>
        /// The argValue can be wrapped with '"' quotes from both sides, in which case the quotes are removed,
        /// but it is not allowed for argValue to contain '"' inside the actual value.
        /// </para>
        /// </summary>
        /// <param name="args">Application command line arguments.</param>
        /// <param name="arg">Name of the command line argument which value should be obtained.</param>
        /// <returns>Value of the specified argument or null if no such argument is found among the given list of arguments.</returns>
        public static string GetValueOf(this string[] args, string arg)
        {
            return args.Where(a => a.StartsWith($"{arg}=")).Select(a => a.Substring($"{arg}=".Length).Replace("\"", string.Empty)).FirstOrDefault();
        }
    }
}