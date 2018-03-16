namespace Stratis.SmartContracts
{
    public interface IPersistentState
    {
        T GetObject<T>(string key);
        void SetObject<T>(string key, T obj);
        ISmartContractMapping<V> GetMapping<V>(string name);
        ISmartContractList<T> GetList<T>(string name);
    }
}