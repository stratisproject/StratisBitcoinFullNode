using Stratis.SmartContracts;

/// <summary>
/// A basic "Hello World" smart contract
/// </summary>
[Deploy]
public class HelloWorld : SmartContract
{
    private string Greeting
    {
        get 
        {
            return this.PersistentState.GetString("Greeting");
        }
        set
        {
            this.PersistentState.SetString("Greeting", value);
        }
    }

    public HelloWorld(ISmartContractState smartContractState)
        : base(smartContractState)
    {
        this.Greeting = "Hello World!";
    }

    public string SayHello()
    {
        return this.Greeting;
    }

}
