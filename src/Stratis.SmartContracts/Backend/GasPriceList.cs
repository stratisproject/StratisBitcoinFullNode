using System;

namespace Stratis.SmartContracts.Backend
{
    /// <summary>
    /// Creates gas spend operations with the correct costs
    /// </summary>
    public static class GasPriceList
    {
        public static GasSpendOperation Create(string opcode, string operand)
        {
            Gas cost;

            //@TODO Add values
            switch (opcode.ToLowerInvariant())
            {
                default:
                    cost = (Gas)1;
                    break;
            }

            return new GasSpendOperation(cost);
        }

        public static Gas StorageOperationCost(byte[] keyBytes, byte[] valueBytes)
        {
            Gas cost = (Gas)(ulong)(20000 * keyBytes.Length + 20000 * valueBytes.Length);
            return cost;
        }
    }
}