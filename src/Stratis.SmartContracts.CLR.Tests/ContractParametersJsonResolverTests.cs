using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ContractParametersJsonResolverTests
    {
        private readonly Network network;
        private readonly ContractParametersContractResolver resolver;

        public ContractParametersJsonResolverTests()
        {
            this.network = new SmartContractsPoARegTest();
            this.resolver = new ContractParametersContractResolver(this.network);
        }

        [Fact]
        public void Address_Json_Outputs_As_Base58String()
        {
            uint160 testUint160 = new uint160(123);
            Address testAddress = testUint160.ToAddress();
            string expectedString = testUint160.ToBase58Address(this.network);

            string jsonOutput = JsonConvert.SerializeObject(testAddress, new JsonSerializerSettings
            {
                ContractResolver = this.resolver
            });
            Assert.Equal(expectedString, jsonOutput.Replace("\"", ""));
        }

        [Fact]
        public void LocalExecutionResult_Outputs_With_Address()
        {
            uint160 testUint160 = new uint160(123);
            Address testAddress = testUint160.ToAddress();
            string expectedString = testUint160.ToBase58Address(this.network);

            var execResult = new LocalExecutionResult
            {
                ErrorMessage = new ContractErrorMessage("Error message"),
                GasConsumed = (Gas) 69,
                Return = testAddress
            };

            string jsonOutput = JsonConvert.SerializeObject(execResult, new JsonSerializerSettings
            {
                ContractResolver = this.resolver
            });
            Assert.Contains($"\"Return\":\"{expectedString}\"", jsonOutput);
        }
    }
}
