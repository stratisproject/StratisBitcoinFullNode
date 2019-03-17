using Stratis.SmartContracts;

[Deploy]
public sealed class NestedLoop : SmartContract
{
    public NestedLoop(ISmartContractState state)
        : base(state)
    {
    }

    public string GetNumbers(int max)
    {
        string result = string.Empty;

        for (int i = 0; i < max; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                if (j != 0)
                {
                    result += ",";
                }
                result += $"{j + 1}";
            }

            result += "; ";
        }

        return result;
    }
}
