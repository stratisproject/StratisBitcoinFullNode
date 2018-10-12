using System;
using TracerAttributes;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Collection of guard methods.
    /// <para>
    /// Guards are typically used at the beginning of a method to protect the body of
    /// the method being called with invalid set of parameters or object states.
    /// </para>
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Asserts that a condition is true.
        /// </summary>
        /// <param name="condition">The condition to assert.</param>
        [NoTrace]
        public static void Assert(bool condition)
        {
            if (!condition)
                throw new Exception("Assertion failed");
        }

        /// <summary>
        /// Checks an object is not null.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="value">The object.</param>
        /// <param name="parameterName">The name of the object.</param>
        /// <returns>The object if it is not null.</returns>
        /// <exception cref="ArgumentNullException">An exception if the object passed is null.</exception>
        [NoTrace]
        public static T NotNull<T>(T value, string parameterName)
        {
            // the parameterName should never be null or empty
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentNullException(parameterName);
            }

            // throw if the value is null
            if (ReferenceEquals(value, null))
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        /// <summary>
        /// Checks a <see cref="string"/> is not null or empty.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <param name="parameterName">The name of the string.</param>
        /// <returns>The string if it is not null or empty.</returns>
        [NoTrace]
        public static string NotEmpty(string value, string parameterName)
        {
            NotNull(value, parameterName);

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException($"The string parameter {parameterName} cannot be empty.");
            }

            return value;
        }
    }
}
