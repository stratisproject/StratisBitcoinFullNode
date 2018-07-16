﻿using Stratis.SmartContracts;

public class CallContract : SmartContract
{
    public CallContract(ISmartContractState state) : base(state)
    {
        Test = "Initial";
    }

    public string Test
    {
        get
        {
            return PersistentState.GetString("Test");
        }
        set
        {
            PersistentState.SetString("Test", value);
        }
    }

    public bool CallOther(string addressString)
    {

        ITransferResult result = TransferFunds(new Address(addressString), 100, new TransferFundsToContract
        {
            ContractMethodName = "IncrementCount"
        });

        return result.Success;
    }

    public bool GetOtherCountValueAndUpdateOurs(string addressString)
    {
        ITransferResult result = TransferFunds(new Address(addressString), 100, new TransferFundsToContract
        {
            ContractMethodName = "get_Count"
        });

        if (result.Success)
            this.NewCount = (int)result.ReturnValue;

        return result.Success;
    }

    public bool Tester(string addressString)
    {
        Test = "Not Initial!";
        ITransferResult result = TransferFunds(new Address(addressString), 0, new TransferFundsToContract
        {
            ContractMethodName = "Callback"
        });
        return (bool)result.ReturnValue;
    }

    public bool Asserter()
    {
        return Test == "Not Initial!";
    }

    public int NewCount { get; set; }
}
