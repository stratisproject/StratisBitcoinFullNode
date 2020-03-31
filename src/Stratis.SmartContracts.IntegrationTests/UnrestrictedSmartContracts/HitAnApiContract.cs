﻿using System.Net.Http;
using Stratis.SmartContracts;

namespace HitAnApi
{
    public class HitAnApiContract : SmartContract
    {
        public HitAnApiContract(ISmartContractState state) : base(state)
        {
        }

        public int CallApi(string apiAddress)
        {
            var client = new HttpClient();

            HttpResponseMessage result = client.GetAsync(apiAddress).Result;

            string httpResult = result.Content.ReadAsStringAsync().Result;

            this.PersistentState.SetInt32("ImportantNumber", httpResult.Length);

            return httpResult.Length;
        }
    }
 
}


