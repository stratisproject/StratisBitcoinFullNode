//using System;
//using System.Collections.Generic;

//namespace Stratis.SmartContracts.State
//{
//    internal class DictionaryStateDb : IStateDb
//    {
//        private const ulong InitialNonce = 0; // TODO: Addd / Get from some kind of chain-level constant config.

//        private Dictionary<Address, AccountState> _accountStates = new Dictionary<Address, AccountState>();

//        public void SetCode(Address address, byte[] code)
//        {
//            _accountStates[address].Code = code;
//        }

//        public byte[] GetCode(Address address)
//        {
//            return _accountStates[address].Code;
//        }

//        public void SetStorageValue(Address address, UInt256 key, UInt256 value)
//        {
//            TODO: Check if we can assume that AccountState will always be initialised here.
//           _accountStates[address].Storage[key] = value;
//        }

//        public UInt256 GetStorageValue(Address address, UInt256 key)
//        {
//            AccountState accountState = GetAccountState(address);

//            return accountState != null ? _accountStates[address].GetStorageValueOrZero(key) : null;
//        }

//        public ulong GetBalance(Address address)
//        {
//            AccountState accountState = GetAccountState(address);
//            return accountState != null ? accountState.Balance : 0;
//        }

//        public ulong AddBalance(Address address, ulong value)
//        {
//            var accountState = GetOrCreateAccountState(address);
//            accountState.Balance += value;
//            return accountState.Balance;
//        }

//        public ulong SubtractBalance(Address address, ulong value)
//        {
//            Assume account always exists
//            var accountState = _accountStates[address];
//            accountState.Balance -= value;
//            return accountState.Balance;
//        }

//        public void IncrementNonce(Address address)
//        {
//            var accountState = _accountStates[address];
//            accountState.Nonce = accountState.Nonce + 1;
//        }

//        public ulong GetNonce(Address address)
//        {
//            AccountState accountState = GetAccountState(address);
//            return accountState != null ? accountState.Nonce : InitialNonce;
//        }

//        public object GetAccountObject(Address address, string key)
//        {
//            AccountState accountState = GetAccountState(address);
//            return accountState != null ? accountState.ObjectStorage[key] : null;
//        }

//        public void SetAccountObject(Address address, string key, object obj)
//        {
//            AccountState accountState = GetAccountState(address);
//            accountState.ObjectStorage[key] = obj;
//        }

//        public AccountState CreateAccount(Address address)
//        {
//            var newAccountState = new AccountState();
//            _accountStates.Add(address, newAccountState);
//            return newAccountState;
//        }

//        private AccountState GetAccountState(Address address)
//        {
//            if (_accountStates.ContainsKey(address))
//                return _accountStates[address];

//            return null;
//        }

//        private AccountState GetOrCreateAccountState(Address address)
//        {
//            AccountState accountState = GetAccountState(address);
//            if (accountState != null)
//                return accountState;

//            return CreateAccount(address);
//        }

//        public void Rewind()
//        {
//            throw new NotImplementedException();
//        }

//        public void Commit()
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
