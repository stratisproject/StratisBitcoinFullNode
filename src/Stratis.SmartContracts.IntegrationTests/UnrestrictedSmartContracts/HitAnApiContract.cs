using System.Net.Http;
using Newtonsoft.Json;
using Stratis.SmartContracts;

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

        this.PersistentState.SetInt32("ImportantNumber", (int) result.StatusCode);

        return httpResult.Length;
    }

    public string CallJsonApi()
    {
        return StructGetter.GetTitle(1);
    }
}

public static class StructGetter
{
    public static string GetTitle(int id)
    {
        var client = new HttpClient();

        HttpResponseMessage result = client.GetAsync("https://jsonplaceholder.typicode.com/todos/1").Result;
        string httpResult = result.Content.ReadAsStringAsync().Result;

        Result resultDeserialized = JsonConvert.DeserializeObject<Result>(httpResult);

        return resultDeserialized.Title;
    }

    public class Result
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}


