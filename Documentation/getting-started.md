

Getting started
---------------

*On the current version of the node*


1. Install .NET Core [1.0.0-preview2-1-003177](https://github.com/dotnet/core/blob/master/release-notes/download-archives/preview-download.md)

2. Clone the reposiroty 
```
git clone https://github.com/stratisproject/StratisBitcoinFullNode.git  
```

3. Checkout the develop branch (or master without latest changes)
```
cd StratisBitcoinFullNode
git checkout develop
```

4. Restore the project packages  
```
dotnet restore
```

5. Run the node on Main net
```
cd Stratis.BitcoinD
dotnet run
```

Docker Containers
-------------------

Two containers are available [here](https://hub.docker.com/u/stratisplatform/dashboard/)

- stratis-node: Run on the Bitcoin Main or Test networks
- stratis-node-sim: Join our simulation network
