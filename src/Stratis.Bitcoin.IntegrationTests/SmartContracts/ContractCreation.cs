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
        var result = Create<Cat>(0, new object[] { CatCounter });
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


    public Cat(ISmartContractState smartContractState, int catNumber) : base(smartContractState)
    {
        CatNumber = catNumber;
        Log(new CatCreated { CatNumber = catNumber });
    }

    public struct CatCreated
    {
        public int CatNumber;
    }
}