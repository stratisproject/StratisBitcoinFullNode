using System;
using Stratis.SmartContracts;

public class MemoryLimit : SmartContract
{
    public MemoryLimit(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void AllowedArray()
    {
        var arr = new int[100];
    }

    public void NotAllowedArray()
    {
        var arr = new int[100_001];
    }

    public void AllowedArrayResize()
    {
        var arr = new int[4];

        Array.Resize(ref arr, 100);
    }

    public void NotAllowedArrayResize()
    {
        var arr = new int[4];

        Array.Resize(ref arr, 100_001);
    }

    public void AllowedStringConstructor()
    {
        var test = new string('a', 100);
    }

    public void NotAllowedStringConstructor()
    {
        var test = new string('a', 100_001);
    }

    public void AllowedToCharArray()
    {
        var test = new string('a', 500);
        test.ToCharArray();
    }

    public void NotAllowedToCharArray()
    {
        var test = new string('a', 50_001);
        test.ToCharArray();
    }
    
    public void AllowedSplit()
    {
        var test = new string('a', 500);
        var test2 = test.Split('a');
    }

    public void NotAllowedSplit()
    {
        var test = new string('a', 50_001);
        var test2 = test.Split('a');
    }

    public void AllowedJoin()
    {
        var test = new char[500];
        string result = string.Join(",", test);
    }

    public void NotAllowedJoin()
    {
        var test = new char[60_000];
        string result = string.Join(",", test);
    }
    
    public void AllowedConcat()
    {
        string test = "1234567890";
        for(int i=0; i< 10; i++)
        {
            test += test;
        }
    }

    public void NotAllowedConcat()
    {
        string test = "1234567890";
        for (int i = 0; i < 100_000; i++)
        {
            test += test;
        }
    }
}
