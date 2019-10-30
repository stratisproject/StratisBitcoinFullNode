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
            this.PersistentState.SetInt32("Index", value);
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
            this.PersistentState.SetInt32("Bounds", value);
        }
    }    
    
    private string Greeting 
    {
        get
        {
            this.Index++;
            if (this.Index >= this.Bounds)
            {
                this.Index = 0;
            }

            return this.PersistentState.GetString("Greeting" + this.Index);
        }   
        set
        {
            this.PersistentState.SetString("Greeting" + this.Bounds, value);
            this.Bounds++;
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
        return this.Greeting;
    }

    public string AddGreeting(string helloMessage)
    {
        this.Greeting = helloMessage;
        return "Added '" + helloMessage + "' as a greeting.";
    }

}
