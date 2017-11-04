What is it used for?
--------------------
IndexStore allows you to find a block, transaction or a transaction's inputs/outputs based on the data that it contains.

It does this using custom pre-defined indexes that are kept up-to-date by the index store as new blocks are discovered.

How is it used?
===============

For example to quickly determine which transaction spent a particular output from another transaction you
can define an index described by the following LINQ expression:

"(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })"

where:
- "(t,b,n)" are the (transaction, block, network) associated with a single transaction.
- the key value for the lookup is represented by "new object[] { i.PrevOut.Hash, i.PrevOut.N }" (identifying an output) and 
- the value being looked up is represented by "t.GetHash()" (the transaction spending the output)

Any index returning more than one value per key is expected to set the "multiValue" indicator to true. For this example it 
is set to false as we only expect one transaction to spend any given output from another transaction and hence there will
only be one value for each key.

The definition of the index is a once-off activity. Once the index has been created it is automaically kept up-to-date and 
you can perform a lookup against the index at any time. See methods listed below.

How does it work?
=================

The IndexStore looks at each transaction being added to the index store and runs the above LINQ query (one per index)
on it to obtain key/value pairs to add to any of the custom pre-defined indexes. The preceding implies that your LINQ
query is expected to operate at transaction level.

Available methods
=================
        
Task<bool> CreateIndex(string name, bool multiValue, string builder, string[] dependencies = null);
Task<bool> DropIndex(string name);
Task<KeyValuePair<string, Index>[]> ListIndexes(Func<KeyValuePair<string, Index>, bool> include = null);

Task<byte[]> Lookup(string indexName, byte[] key);
Task<List<byte[]>> LookupMany(string indexName, byte[] key);
Task<List<byte[]>> Lookup(string indexName, List<byte[]> keys);

IndexStore also supports all the methods available for the Block Store.

RPC
===

RPC has not been fully implemented yet.