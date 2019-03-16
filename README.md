| Windows | Linux | OS X
| :---- | :------ | :---- |
[![Windows build status][1]][2] | [![Linux build status][3]][4] | [![OS X build status][5]][6] | 

[1]: https://ci.appveyor.com/api/projects/status/4nc986aalq07vk3t/branch/master?svg=true
[2]: https://ci.appveyor.com/project/x42/X42-FullNode-UI
[3]: https://travis-ci.org/x42protocol/X42-FullNode-UI.svg?branch=master
[4]: https://travis-ci.org/x42protocol/X42-FullNode-UI
[5]: https://travis-ci.org/x42protocol/X42-FullNode-UI.svg?branch=master
[6]: https://travis-ci.org/x42protocol/X42-FullNode-UI


x42
===============

http://www.x42.tech
A blockchain for entrepreneurs
----------------------------

x42 is an implementation of the Bitcoin protocol in C# on the [.NET Core](https://dotnet.github.io/) platform.  
The node can run on the Bitcoin and x42 networks.  
x42 Bitcoin is based on the [NBitcoin](https://github.com/MetacoSA/NBitcoin) project.  

For Proof of Stake support on the x42 token the node is using [NStratis](https://github.com/stratisproject/NStratis) which is a POS implementation of NBitcoin.  

[.NET Core](https://dotnet.github.io/) is an open source cross platform framework and enables the development of applications and services on Windows, macOS and Linux.  

Join our community on [discord](https://discord.gg/bmYUmjr).

The design
----------

**A Modular Approach**

A Blockchain is made of many components, from a FullNode that validates blocks to a Simple Wallet that track addresses.
The end goal is to develop a set of [Nuget](https://en.wikipedia.org/wiki/NuGet) packages from which an implementer can cherry pick what he needs.

* **NBitcoin**
* **x42.Bitcoin.Core**  - The bare minimum to run a pruned node.
* **x42.Bitcoin.Store** - Store and relay blocks to peers.
* **x42.Bitcoin.MemoryPool** - Track pending transaction.
* **x42.Bitcoin.Wallet** - Send and Receive coins
* **x42.Bitcoin.Miner** - POS or POW
* **x42.Bitcoin.Explorer**


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

Running a FullNode
------------------

Our full node is currently in alpha.  

```
git clone https://github.com/x42protocol/X42-FullNode.git
cd X42-FullNode\src

dotnet build

```

To run on the Bitcoin network:
```
cd Stratis.BitcoinD
dotnet run
```  

To run on the x42 network:
```
cd x42.x42D
dotnet run
```  

Getting Started Guide
-----------
More details on getting started are available [here](https://github.com/x42protocol/X42-FullNode/blob/master/Documentation/getting-started.md)

Development
-----------
Up for some blockchain development?

Check this guides for more info:
* [Contributing Guide](Documentation/contributing.md)
* [Coding Style](Documentation/coding-style.md)
* [Wiki Page](https://x42platform.atlassian.net/wiki/spaces/WIKI/overview)

There is a lot to do and we welcome contributers developers and testers who want to get some Blockchain experience.
You can find tasks at the issues/projects or visit us on [discord](https://discord.gg/bmYUmjr).

Testing
-------
* [Testing Guidelines](Documentation/testing-guidelines.md)

CI build
-----------

We use [AppVeyor](https://www.appveyor.com/) for our CI build and to create nuget packages.
Every time someone pushes to the master branch or create a pull request on it, a build is triggered and new nuget packages are created.

To skip a build, for example if you've made very minor changes, include the text **[skip ci]** or **[ci skip]** in your commits' comment (with the squared brackets).

If you want get the :sparkles: latest :sparkles: (and unstable :bomb:) version of the nuget packages here: 
* [x42.Bitcoin](https://ci.appveyor.com/api/projects/x42/x42bitcoinfullnode/artifacts/nuget/x42.Bitcoin.1.0.7-alpha.nupkg?job=Configuration%3A%20Release)
