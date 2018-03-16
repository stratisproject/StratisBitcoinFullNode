namespace Stratis.SmartContracts
{
    public interface ISmartContractMapping<V>
    {
        void Put(string key, V value);
        V Get(string key);
        V this[string key] { get; set; }
    }
}