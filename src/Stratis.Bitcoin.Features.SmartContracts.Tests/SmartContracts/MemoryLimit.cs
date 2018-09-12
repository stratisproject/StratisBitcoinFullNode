using System;
using Stratis.SmartContracts;

public class MemoryLimit : SmartContract
{
    public MemoryLimit(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void AllowedArray()
    {
        var arr = Array.CreateInstance(typeof(int), 500);
        //var arr = new int[100];
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
        //Array.C
    }
                          //.Member(nameof(string.Join), Allowed, CollectedEnumerableArgumentRewriter.Default)
                          //.Member(nameof(string.Concat), Allowed, CollectedEnumerableArgumentRewriter.Default)

}
