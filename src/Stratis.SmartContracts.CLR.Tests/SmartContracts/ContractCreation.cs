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
        UpdateLastCreatedCat(result.NewContractAddress);
    }

    public void CreateCatIsContract()
    {
        var result = Create<Cat>(0, new object[] { CatCounter });
        UpdateLastCreatedCat(result.NewContractAddress);
        PersistentState.SetBool("IsContract", PersistentState.IsContract(result.NewContractAddress));
    }

    public void CreateCatWithFunds()
    {
        var result = Create<Cat>(Balance, new object[] { CatCounter });
        UpdateLastCreatedCat(result.NewContractAddress);
    }

    private void UpdateLastCreatedCat(Address newContractAddress)
    {
        CatCounter++;
        LastCreatedCat = newContractAddress;
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
        [Index]
        public int CatNumber;
    }
}