

Getting started
---------------

*On the current version of the node*


1. Install [.NET Core](https://www.microsoft.com/net/download)

2. Clone the reposiroty 
```
git clone https://github.com/stratisproject/StratisBitcoinFullNode.git  
```

3. Checkout the master branch
```
cd StratisBitcoinFullNode
git checkout master
```

4. Restore the project packages  
```
dotnet restore
```
5. Go into the daemon folder  
For BTC   ```cd Stratis.BitcoinD```  
For STRAT ```cd Stratis.StratisD```

6. Run the node on Main net
```
dotnet run
```

Docker Containers
-------------------

Two containers are available [here](https://hub.docker.com/u/stratisplatform/)

- stratis-node: Run on the Bitcoin Main or Test networks
- stratis-node-sim: Join our simulation network
