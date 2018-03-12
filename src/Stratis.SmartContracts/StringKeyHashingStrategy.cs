using System.Linq;
using System.Text;
using Stratis.SmartContracts.Hashing;

namespace Stratis.SmartContracts
{
    public class StringKeyHashingStrategy
    {
        public static StringKeyHashingStrategy Default = new StringKeyHashingStrategy();

        public string Hash(params string[] args)
        {
            byte[] toHash = args.SelectMany(u => Encoding.UTF8.GetBytes(u)).ToArray();

            return Encoding.UTF8.GetString(HashHelper.Keccak256(toHash));
        }
    }
}