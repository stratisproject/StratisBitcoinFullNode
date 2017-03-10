Stratis Bitcoin
===============

https://stratisplatform.com

Bitcoin Implementation in C#
----------------------------

Stratis is implementation the Bitcoin protocol in C# on the [.NET Core](https://dotnet.github.io/) platform.  
The daemon is a full implementation of the Stratis Bitcoin Full Node.  
Stratis Bitcoins is based on the [NBitcoin](https://github.com/MetacoSA/NBitcoin) project.

The [.NET Core](https://dotnet.github.io/) is cross-platform, supporting Windows, macOS and Linux.    
Join the community at [slack](https://stratisplatform.slack.com)

Running A FullNode 
------------------

Our full node is currently in alpha, to run on the main bitcoin network:

```
git clone https://github.com/stratisproject/StratisBitcoinFullNode.git
dotnet restore
dotnet run
```

Whats Next 
----------

We plan to add many more features on top of the Stratis Bitcoin blockchain:  
POS/DPOS, Sidechains, Private/Permissioned blockchain, Compiled Smart Contracts, NTumbleBit/Breeze wallet and more..  
And build tailored Blockchian solutions for enterprices.

**A Modular Approach**

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

Check this guides for more info.
* [Contributing Guide](Documentation/contributing.md)
* [Coding Style](Documentation/coding-style.md.md)

There is a alot to do and we welcome contributers developers and testers who want to get some Blockchin experiance.  
You can find tasks at the issues/projects or visit our [C# dev](https://stratisplatform.slack.com/messages/csharp_development/) slack channel

Testing
-------


