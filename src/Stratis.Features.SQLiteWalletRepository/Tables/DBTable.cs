using System;
using System.Globalization;
using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class DBTable
    {
        // Intended to handle string, int, long and decimal.
        internal static string DBParameter(object prop)
        {
            if (prop == null)
                return "NULL";

            switch (prop)
            {
                case string strProp:
                    return $"'{strProp.Replace("'", "''")}'";

                case decimal decProp:
                    return decProp.ToString("0.#############################", CultureInfo.InvariantCulture);

                case int intProp:
                    return intProp.ToString("F0", CultureInfo.InvariantCulture);

                case long longProp:
                    return longProp.ToString("F0", CultureInfo.InvariantCulture);

                case uint256 uint256pProp:
                    return $"'{uint256pProp}'";

            }

            throw new InvalidOperationException($"Unsupported data type passed to {nameof(DBParameter)} method");
        }
    }
}
