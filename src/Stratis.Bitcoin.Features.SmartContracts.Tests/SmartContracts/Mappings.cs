using Stratis.SmartContracts;

public class Mappings : SmartContract
{
    public struct Example
    {
        public string Id;
        public IScMapping<string> Values;
    }

    public IScMapping<string> Mapping => this.PersistentState.LoadMapping<string>("Mapping");

    public IScList<string> List => this.PersistentState.LoadList<string>("List");

    public IScMapping<IScList<string>> MappingOfLists => this.PersistentState.LoadMapping<IScList<string>>("MappingOfLists");

    public IScList<Example> ListOfStructsWithMappings => this.PersistentState.LoadList<Example>("ListOfStructsWithMappings");

    public Mappings(ISmartContractState smartContractState) : base(smartContractState) { }

    public string BasicMappingTest()
    {
        Mapping["Key1"] = "Value1";
        Mapping["Key2"] = "Value2";
        return Mapping["Key1"];
    }

    public string BasicListTest()
    {
        List.Push("Value1");
        List.Push("Value2");
        return List[0];
    }

    public string MappingOfListsTest()
    {
        IScList<string> aList = MappingOfLists["Key1"];
        aList.Push("Value1");
        IScList<string> anotherList = MappingOfLists["Key2"];
        anotherList.Push("Value2");
        return MappingOfLists["Key1"][0];
    }

    public string ListOfStructsWithMappingsTest()
    {
        var struct1 = new Example();
        struct1.Id = "Id1";
        struct1.Values = this.PersistentState.LoadMapping<string>("Mapping1");
        struct1.Values.Put("Key1", "Value1");
        ListOfStructsWithMappings.Push(struct1);

        var getStructBack = ListOfStructsWithMappings[0];
        return getStructBack.Values["Key1"];
    }



}