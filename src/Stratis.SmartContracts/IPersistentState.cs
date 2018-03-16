namespace Stratis.SmartContracts
{
    /// <summary>
    /// Contract that specifies how items are retrieved/saved in a smart contract. 
    /// </summary>
    public interface IPersistentState
    {
        T GetObject<T>(string key);
        void SetObject<T>(string key, T obj);
        ISmartContractMapping<V> GetMapping<V>(string name);
        ISmartContractList<T> GetList<T>(string name);
    }
}