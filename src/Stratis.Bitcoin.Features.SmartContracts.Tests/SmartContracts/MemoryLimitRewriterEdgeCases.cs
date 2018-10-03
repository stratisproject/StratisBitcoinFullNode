using System;
using Stratis.SmartContracts;

[Deploy]
public sealed class MemoryLimitRewriterEdgeCases : SmartContract
{
    public MemoryLimitRewriterEdgeCases(ISmartContractState state)
        : base(state)
    {
    }

    public void EdgeCase1()
    {
        string text = string.Empty;
        int num = 0;
        while (true)
        {
            if (num > 6)
            {
                break;
            }
            text = text + string.Format("{0}", num + 1);
            text = text + "; ";
            num++;
        }
    }
}
