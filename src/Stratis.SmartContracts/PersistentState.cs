using Stratis.SmartContracts.State;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts
{
    public static class PersistentState
    {
        internal static IStateDb StateDb { get; private set; }

        private static uint160 _contractAddress;

        private static uint _counter;

        internal static void SetDbAndAddress(IStateDb stateDb, uint160 contractAddress)
        {
            StateDb = stateDb;
            _contractAddress = contractAddress;
        }

        internal static void SetAddress(uint160 contractAddress)
        {
            _contractAddress = contractAddress;
        }

        public static T GetObject<T>(string key)
        {
            return StateDb.GetObject<T>(_contractAddress, key);
        }

        public static void SetObject<T>(string key, T obj)
        {
            StateDb.SetObject<T>(_contractAddress, key, obj);
        }

        public static T GetObject<T>(object key)
        {
            return StateDb.GetObject<T>(_contractAddress, key);
        }

        public static void SetObject<T>(object key, T obj)
        {
            StateDb.SetObject<T>(_contractAddress, key, obj);
        }

        public static SmartContractMapping<K,V> GetMapping<K, V>()
        {
            return new SmartContractMapping<K, V>(_counter++);
        }

        public static SmartContractList<T> GetList<T>()
        {
            return new SmartContractList<T>(_counter++);
        }

        internal static void ResetCounter()
        {
            _counter = 0;
        }
    }
}
