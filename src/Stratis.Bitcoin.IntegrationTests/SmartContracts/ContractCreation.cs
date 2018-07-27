using Stratis.SmartContracts;

[Deploy]
public class CatOwner : SmartContract
{
    private int CatCounter
    {
        get
        {
            return PersistentState.GetInt32(nameof(CatCounter));
        }
        set
        {
            PersistentState.SetInt32(nameof(CatCounter), value);
        }
    }

    private Address LastCreatedCat
    {
        get
        {
            return PersistentState.GetAddress(nameof(LastCreatedCat));
        }
        set
        {
            PersistentState.SetAddress(nameof(LastCreatedCat), value);
        }
    }


    public CatOwner(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void CreateCat()
    {
        var result = Create<Cat>(new object[]{ CatCounter });
        CatCounter++;
        LastCreatedCat = result.NewContractAddress;
    }

}

public class Cat : SmartContract
{
    private int CatNumber
    {
        get
        {
            return PersistentState.GetInt32(nameof(CatNumber));
        }
        set
        {
            PersistentState.SetInt32(nameof(CatNumber), value);
        }
    }


    protected Cat(ISmartContractState smartContractState, int catNumber) : base(smartContractState)
    {
        CatNumber = catNumber;
    }


}