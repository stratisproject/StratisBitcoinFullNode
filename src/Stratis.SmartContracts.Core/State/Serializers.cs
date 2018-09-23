using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// When we store contract data and their UTXO balances, we are serializing those data types to bytes and storing them
    /// in a byte[]/byte[] K/V store.
    /// This class provides access to serializers used during this process.
    /// </summary>
    public static class Serializers
    {
        public static AccountStateSerializer AccountSerializer { get; } = new AccountStateSerializer();
        public static ContractUnspentOutputSerializer ContractOutputSerializer { get; } = new ContractUnspentOutputSerializer();

        public class AccountStateSerializer : ISerializer<AccountState, byte[]>
        {
            public byte[] Serialize(AccountState obj)
            {
                return obj.ToBytes();
            }

            public AccountState Deserialize(byte[] stream)
            {
                return stream == null || stream.Length == 0 ? null : new AccountState(stream);
            }
        }

        public class ContractUnspentOutputSerializer : ISerializer<ContractUnspentOutput, byte[]>
        {
            public byte[] Serialize(ContractUnspentOutput obj)
            {
                return obj.ToBytes();
            }

            public ContractUnspentOutput Deserialize(byte[] stream)
            {
                return stream == null || stream.Length == 0 ? null : new ContractUnspentOutput(stream);
            }
        }

        public class NoSerializer<T> : ISerializer<T, T>
        {
            public T Serialize(T obj)
            {
                return obj;
            }

            public T Deserialize(T stream)
            {
                return stream;
            }
        }
    }
}
