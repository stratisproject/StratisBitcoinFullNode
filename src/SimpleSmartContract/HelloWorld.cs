using System;
using Stratis.SmartContracts;

namespace SimpleSmartContract
{
    public class HelloWorld : SmartContract
    {
        public HelloWorld(ISmartContractState state) : base(state)
        {
        }
        
        public string SayHello()
        {
            return this.Greeting;
        }
        
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
    }
}