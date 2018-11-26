using System;
using Stratis.SmartContracts;

[Deploy]
public sealed class MultipleIfElseBlocks : SmartContract
{
    public MultipleIfElseBlocks(ISmartContractState state)
        : base(state)
    {
    }

    public void PersistNormalizeValue(string value)
    {
        if (value == "a")
        {
            this.PersistedValue = "A";
        }
        else if (value.Contains("b"))
        {
            this.PersistedValue = "B";
        }
        else if (value.StartsWith("c", StringComparison.Ordinal))
        {
            this.PersistedValue = "C";
        }
        else
        {
            this.PersistedValue = "D";
        }
    }

    public string PersistedValue
    {
        get { return this.PersistentState.GetString(nameof(this.PersistedValue)); }
        set { this.PersistentState.SetString(nameof(this.PersistedValue), value); }
    }
}
