using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Serialization;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class JObjectParameterInfoMapperTests
    {
        [Fact]
        public void Map_Method_Params_Success()
        {
            var code = @"
using Stratis.SmartContracts;
public class Test
{
    public void AcceptsAllParams(bool b, byte bb, byte[] ba, char c, string s, uint ui, ulong ul, int i, long l, Address a) {}
}
";
            var compiled = ContractCompiler.Compile(code).Compilation;
            var assembly = Assembly.Load(compiled);
            var method = assembly.ExportedTypes.First().GetMethod("AcceptsAllParams");

            // The jObject as we expect it to come from swagger.
            var jObject = JObject.FromObject(new
            {
                b = "true", // TODO check swagger serialization of bools
                bb = "DD",
                ba = "AABB",
                c = 'a',
                s = "Test",
                ui = 12,
                ul = 123123128823,
                i = 257,
                l = 1238457438573495346,
                a = "address"
            });

            var mapper = new JObjectParameterInfoMapper();

            var mapped = mapper.Map(jObject, method.GetParameters());

            // Check the order and type of each param is correct.
            Assert.Equal(10, mapped.Length);
            Assert.Equal($"{(int) MethodParameterDataType.Bool}#true", mapped[0]);
            Assert.Equal($"{(int)MethodParameterDataType.Byte}#DD", mapped[1]);
            Assert.Equal($"{(int)MethodParameterDataType.ByteArray}#AABB", mapped[2]);
            Assert.Equal($"{(int)MethodParameterDataType.Char}#a", mapped[3]);
            Assert.Equal($"{(int)MethodParameterDataType.String}#Test", mapped[4]);
            Assert.Equal($"{(int)MethodParameterDataType.UInt}#12", mapped[5]);
            Assert.Equal($"{(int)MethodParameterDataType.ULong}#123123128823", mapped[6]);
            Assert.Equal($"{(int)MethodParameterDataType.Int}#257", mapped[7]);
            Assert.Equal($"{(int)MethodParameterDataType.Long}#1238457438573495346", mapped[8]);
            Assert.Equal($"{(int)MethodParameterDataType.Address}#address", mapped[9]);

        }
    }
}