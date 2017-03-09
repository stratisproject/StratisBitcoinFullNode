Stratis Bitcoin
===============

https://stratisplatform.com

Bitcoin Implementation in C#
----------------------------

Stratis is implementation the Bitcoin protocol in C# on the .NET Core platform.  
The daemon is a full implementation of the Stratis Bitcoin Full Node.  
Stratis Bitcoins is based on the [NBitcoin](https://github.com/MetacoSA/NBitcoin) project.

  
We're building the Bitcoin protocol in C#, 
but plan to add many more features on top of Bitcoin: 
Adding POS support, Compiled Smart Contracts, NTumbleBit and Breeze wallet.


A Modular Approach 
------------------
A Blockchain is made of many components, from a FullNode that validates blocks to a Simple Wallet that track addresses.
The end goal is to develop a set of [Nget](https://en.wikipedia.org/wiki/NuGet) packages where an implementer can cheery pick from.

* **NBitcoin**
* **Stratis.Bitcoin.Core**  - The bare minimum to run a pruned node.
* **Stratis.Bitcoin.Store** - Store and relay blocks to peers.
* **Stratis.Bitcoin.MemoryPool** - Track pending transaction.
* **Stratis.Bitcoin.Wallet** - Send and Receive coins
* **Stratis.Bitcoin.Miner** - POS or POW
* **Stratis.Bitcoin.Explorer**


Create a Blockchain in a netcore style programing
```
 var fullnode = new FullNodeBuilder()  
      .Configure(MainNet)
      .UseStore()  
      .UseMemPool()  
      .UseWallet()  
      .AddDashboard()  
 fullnode.Start()
```

Development
-----------
Up for some blockchain development? 
We welcome contributers developers and testers 

Check this guides for more info.
* [Contributing Guide](Documentation/contributing.md)
* [Coding Style](Documentation/coding-style.md.md)


Testing
-------


