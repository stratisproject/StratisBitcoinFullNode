using System.Linq;
using System.Text;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core
{
    public class StringKeyHashingStrategy
    {
        public static StringKeyHashingStrategy Default = new StringKeyHashingStrategy();

        public string Hash(params string[] args)
        {
            byte[] toHash = args.SelectMany(u => Encoding.UTF8.GetBytes((string) u)).ToArray();

            return Encoding.UTF8.GetString(HashHelper.Keccak256(toHash));
        }
    }
}