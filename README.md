Stratis Bitcoin
===============

https://stratisplatform.com

Bitcoin Implementation in C#
----------------------------

Stratis is an implementation of the Bitcoin protocol in C# on the [.NET Core](https://dotnet.github.io/) platform.  
The daemon is a full implementation of the Stratis Bitcoin Full Node.  
Stratis Bitcoin is based on the [NBitcoin](https://github.com/MetacoSA/NBitcoin) project.  
At some point the code will be forked to support the Stratis Token using [NStratis](https://github.com/stratisproject/NStratis) which is a POS implementation of NBitcoin.

[.NET Core](https://dotnet.github.io/) is an open source cross platform framework and enables the development of applications and services on Windows, macOS and Linux.    
Join our community on [slack](https://stratisplatform.slack.com).

Running a FullNode 
------------------

Our full node is currently in alpha. To run it on the main bitcoin network:

```
git clone https://github.com/stratisproject/StratisBitcoinFullNode.git
dotnet restore
dotnet run
```

What's Next 
----------

We plan to add many more features on top of the Stratis Bitcoin blockchain:  
POS/DPOS, Sidechains, Private/Permissioned blockchain, Compiled Smart Contracts, NTumbleBit/Breeze wallet and more...  

**A Modular Approach**

A Blockchain is made of many components, from a FullNode that validates blocks to a Simple Wallet that track addresses.
The end goal is to develop a set of [Nuget](https://en.wikipedia.org/wiki/NuGet) packages from which an implementer can cheery pick what he needs.

* **NBitcoin**
* **Stratis.Bitcoin.Core**  - The bare minimum to run a pruned node.
* **Stratis.Bitcoin.Store** - Store and relay blocks to peers.
* **Stratis.Bitcoin.MemoryPool** - Track pending transaction.
* **Stratis.Bitcoin.Wallet** - Send and Receive coins
* **Stratis.Bitcoin.Miner** - POS or POW
* **Stratis.Bitcoin.Explorer**


Create a Blockchain in a .NET Core style programming
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

Check this guides for more info:
* [Contributing Guide](Documentation/contributing.md)
* [Coding Style](Documentation/coding-style.md)

There is a lot to do and we welcome contributers developers and testers who want to get some Blockchain experience.  
You can find tasks at the issues/projects or visit our [C# dev](https://stratisplatform.slack.com/messages/csharp_development/) slack channel.

Testing
-------
* [Testing Guidelines](Documentation/testing-guidelines.md)

