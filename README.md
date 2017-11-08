| Windows | Linux | OS X
| :---- | :------ | :---- |
[![Windows build status][1]][2] | [![Linux build status][3]][4] | [![OS X build status][5]][6] | 

[1]: https://ci.appveyor.com/api/projects/status/451tv98n7xvxm5ol/branch/master?svg=true
[2]: https://ci.appveyor.com/project/stratis/stratisbitcoinfullnode
[3]: https://travis-ci.org/stratisproject/StratisBitcoinFullNode.svg?branch=master
[4]: https://travis-ci.org/stratisproject/StratisBitcoinFullNode
[5]: https://travis-ci.org/stratisproject/StratisBitcoinFullNode.svg?branch=master
[6]: https://travis-ci.org/stratisproject/StratisBitcoinFullNode


Stratis Bitcoin
===============

https://stratisplatform.com

Bitcoin Implementation in C#
----------------------------

Stratis is an implementation of the Bitcoin protocol in C# on the [.NET Core](https://dotnet.github.io/) platform.  
The node can run on the Bitcoin and Stratis networks.  
Stratis Bitcoin is based on the [NBitcoin](https://github.com/MetacoSA/NBitcoin) project.  

For Proof of Stake support on the Stratis token the node is using [NStratis](https://github.com/stratisproject/NStratis) which is a POS implementation of NBitcoin.  

[.NET Core](https://dotnet.github.io/) is an open source cross platform framework and enables the development of applications and services on Windows, macOS and Linux.  
Join our community on [slack](https://stratisplatform.slack.com).  

The design
----------

**A Modular Approach**

A Blockchain is made of many components, from a FullNode that validates blocks to a Simple Wallet that track addresses.
The end goal is to develop a set of [Nuget](https://en.wikipedia.org/wiki/NuGet) packages from which an implementer can cherry pick what he needs.

* **NBitcoin**
* **Stratis.Bitcoin.Core**  - The bare minimum to run a pruned node.
* **Stratis.Bitcoin.Store** - Store and relay blocks to peers.
* **Stratis.Bitcoin.MemoryPool** - Track pending transaction.
* **Stratis.Bitcoin.Wallet** - Send and Receive coins
* **Stratis.Bitcoin.Miner** - POS or POW
* **Stratis.Bitcoin.Explorer**


Create a Blockchain in a .NET Core style programming
```
  var node = new FullNodeBuilder()
   .UseNodeSettings(nodeSettings)
   .UseConsensus()
   .UseBlockStore()
   .UseMempool()
   .AddMining()
   .AddRPC()
   .Build();

  node.Run();
```

What's Next
----------

We plan to add many more features on top of the Stratis Bitcoin blockchain:
Sidechains, Private/Permissioned blockchain, Compiled Smart Contracts, NTumbleBit/Breeze wallet and more...

Running a FullNode
------------------

Our full node is currently in alpha.  

```
git clone https://github.com/stratisproject/StratisBitcoinFullNode.git  
cd StratisBitcoinFullNode\src

dotnet restore
dotnet build

```

To run on the Bitcoin network: ``` Stratis.BitcoinD\dotnet run ```  
To run on the Stratis network: ``` Stratis.StratisD\dotnet run ```  

See more details [here](https://github.com/stratisproject/StratisBitcoinFullNode/blob/master/Documentation/getting-started.md)

Development
-----------
Up for some blockchain development?

Check this guides for more info:
* [Contributing Guide](Documentation/contributing.md)
* [Coding Style](Documentation/coding-style.md)
* [Wiki Page](https://stratisplatform.atlassian.net/wiki/spaces/WIKI/overview)

There is a lot to do and we welcome contributers developers and testers who want to get some Blockchain experience.
You can find tasks at the issues/projects or visit our [C# dev](https://stratisplatform.slack.com/messages/csharp_development/) slack channel.

Testing
-------
* [Testing Guidelines](Documentation/testing-guidelines.md)

CI build
-----------

We use [AppVeyor](https://www.appveyor.com/) for our CI build and to create nuget packages.
Every time someone pushes to the master branch or create a pull request on it, a build is triggered and new nuget packages are created.

To skip a build, for example if you've made very minor changes, include the text **[skip ci]** or **[ci skip]** in your commits' comment (with the squared brackets).

If you want get the :sparkles: latest :sparkles: (and unstable :bomb:) version of the nuget packages here: [Stratis.Bitcoin](https://ci.appveyor.com/api/projects/stratis/stratisbitcoinfullnode/artifacts/nuget/Stratis.Bitcoin.1.0.2-alpha.nupkg?job=Configuration%3A%20Release) and [Stratis.Bitcoin.Common](https://ci.appveyor.com/api/projects/stratis/stratisbitcoinfullnode/artifacts/nuget/Stratis.Bitcoin.Common.1.0.0.nupkg?job=Configuration%3A%20Release).


