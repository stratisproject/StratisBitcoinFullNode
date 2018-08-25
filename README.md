<div align="center">
	<br>
  <img src="https://city-chain.org/images/logo/city-chain-gold-100x.png" alt="modern-normalize">
	<br>
	<br>
</div>

City Chain
===============

Offical homepage:
https://city-chain.org

Blockchain for the Smart City Platform
----------------------------

[![VSTS build status][1]][2]

[1]: https://citychain.visualstudio.com/city-chain/_apis/build/status/3?branch=master
[2]: https://citychain.visualstudio.com/city-chain/_build/latest?definitionId=3&branch=master

[![Documentation build status][3]][4]

[3]: https://ci.appveyor.com/api/projects/status/xs9789ye8ulu29j3/branch/master?svg=true
[4]: https://ci.appveyor.com/project/citychain/city-chain

City Chain is a blockchain implementation that supports the Bitcoin protocol in C# and runs on [.NET Core](https://dotnet.github.io/), and is based on the [Stratis](https://github.com/stratisproject) and [NBitcoin](https://github.com/MetacoSA/NBitcoin) source code.

The City Chain is built to be the foundation network for the Smart City Platform.

City Chain is a Proof-of-Stake (PoS) blockchain, on which individuals who provide staking ("mining") are randomly selected to perform the 
task of signing the individual blocks in the chain. For this service, the individuals receive City Coin (CTY). City Coin is the currency on the 
City Chain and Smart City Platform. The Smart City Platform will support different crypto-currencies, not just City Coin.

Run City Chain
------------------

The app is currently in alpha, and must be run from source code:

```
git clone https://github.com/CityChainFoundation/city-chain.git
cd city-chain\src

dotnet restore
dotnet build
```

To run on the test network:
```
cd CityChain
dotnet run -testnet
```

Getting Started Guide
-----------
More details on getting started are available [here](Documentation/getting-started.md)

Development
-----------
Up for some blockchain development?

Check this guides for more info:
* [Contributing Guide](Documentation/contributing.md)
* [Coding Style](Documentation/coding-style.md)
* [Wiki Page](https://stratisplatform.atlassian.net/wiki/spaces/WIKI/overview)

There is a lot to do and we welcome contributers developers and testers who want to get some Blockchain experience.

Specification and API reference for source code is published to [https://citychainfoundation.github.io/city-chain/](https://citychainfoundation.github.io/city-chain/).

Testing
-------
* [Testing Guidelines](Documentation/testing-guidelines.md)

CI build
-----------

We use [AppVeyor](https://www.appveyor.com/) for our CI build and to create nuget packages.
Every time someone pushes to the master branch or create a pull request on it, a build is triggered and new nuget packages are created.

To skip a build, for example if you've made very minor changes, include the text **[skip ci]** or **[ci skip]** in your commits' comment (with the squared brackets).
