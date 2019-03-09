using Stratis.SmartContracts;

/// <summary>
/// An extension to the "Hello World" smart contract
/// </summary>
[Deploy]
public class HelloWorld2 : SmartContract
{
    private int Index 
    {
        get
        {
            return this.PersistentState.GetInt32("Index");
        }   
        set
        {
            PersistentState.SetInt32("Index", value);
        }
    }    

    private int Bounds 
    {
        get
        {
            return this.PersistentState.GetInt32("Bounds");
        }   
        set
        {
            PersistentState.SetInt32("Bounds", value);
        }
    }    
    
    private string Greeting 
    {
        get
        {
            Index++;
            if (Index >= Bounds)
            {
                Index = 0;
            }

            return this.PersistentState.GetString("Greeting" + Index);
        }   
        set
        {
            PersistentState.SetString("Greeting" + Bounds, value);
            Bounds++;
        }
    }

    public HelloWorld2(ISmartContractState smartContractState) : base(smartContractState)
    {
        this.Bounds = 0;
        this.Index = -1;
        this.Greeting = "Hello World!";
    }

    public string SayHello()
    {
        return Greeting;
    }

    public string AddGreeting(string helloMessage)
    {
        Greeting = helloMessage;
        return "Added '" + helloMessage + "' as a Greeting";
    }

}
