using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    public static class Guard
    {
		public static void Assert(bool v)
		{
			if (!v)
				throw new Exception("Assertion failed");
		}

		public static T NotNull<T>(T value, string parameterName)
		{
			if (ReferenceEquals(value, null))
			{
				NotEmpty(parameterName, nameof(parameterName));

				throw new ArgumentNullException(parameterName);
			}

			return value;
		}

		public static T NotNull<T>(
			T value,
			string parameterName,
			string propertyName)
		{
			if (ReferenceEquals(value, null))
			{
				NotEmpty(parameterName, nameof(parameterName));
				NotEmpty(propertyName, nameof(propertyName));

				throw new ArgumentException(propertyName, parameterName);
			}

			return value;
		}

		public static IReadOnlyList<T> NotEmpty<T>(IReadOnlyList<T> value, string parameterName)
		{
			NotNull(value, parameterName);

			if (value.Count == 0)
			{
				NotEmpty(parameterName, nameof(parameterName));

				throw new ArgumentException(parameterName);
			}

			return value;
		}

		public static string NotEmpty(string value, string parameterName)
		{
			Exception e = null;
			if (ReferenceEquals(value, null))
			{
				e = new ArgumentNullException(parameterName);
			}
			else if (value.Trim().Length == 0)
			{
				e = new ArgumentException(parameterName);
			}

			if (e != null)
			{
				NotEmpty(parameterName, nameof(parameterName));

				throw e;
			}

			return value;
		}

		public static string NullButNotEmpty(string value, string parameterName)
		{
			if (!ReferenceEquals(value, null)
				&& (value.Length == 0))
			{
				NotEmpty(parameterName, nameof(parameterName));

				throw new ArgumentException(parameterName);
			}

			return value;
		}

		public static IReadOnlyList<T> HasNoNulls<T>(IReadOnlyList<T> value, string parameterName)
			where T : class
		{
			NotNull(value, parameterName);

			if (value.Any(e => e == null))
			{
				NotEmpty(parameterName, nameof(parameterName));

				throw new ArgumentException(parameterName);
			}

			return value;
		}

		public static Type ValidEntityType(Type value, string parameterName)
		{
			if (!value.GetTypeInfo().IsClass)
			{
				NotEmpty(parameterName, nameof(parameterName));

				throw new ArgumentException(parameterName);
			}

			return value;
		}
	}
}
