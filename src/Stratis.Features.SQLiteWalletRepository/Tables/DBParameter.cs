using System;
using System.Globalization;
using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    public static class DBParameter
    {
        // Creates a culture-neutral string for inserting an object's value into an SQL command.
        // Strings generated from numbers use a period ('.') decimal separator and don't contain commas or any other culture-specific formatting.
        internal static string Create(object prop)
        {
            if (prop == null)
                return "NULL";

            switch (prop)
            {
                case string strProp:
                    return $"'{strProp.Replace("'", "''")}'";

                case int intProp:
                    return intProp.ToString("F0", CultureInfo.InvariantCulture);

                case long longProp:
                    return longProp.ToString("F0", CultureInfo.InvariantCulture);

                case uint256 uint256pProp:
                    return $"'{uint256pProp}'";

            }

            throw new InvalidOperationException($"Unsupported data type passed to {nameof(Create)} method");
        }
    }
}
