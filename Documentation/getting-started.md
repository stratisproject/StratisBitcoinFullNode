
# Getting started - Building and running a x42 Full Node 

---------------

## Supported Platforms

* <b>Windows</b> - works from Windows 7 and later, on both x86 and x64 architecture. Most of the development and testing is happening here.
* <b>Linux</b> - works and Ubuntu 14.04 and later (x64). It's been known to run on some other distros so your mileage may vary.
* <b>MacOS</b> - works from OSX 10.12 and later. 

## Prerequisites

To install and run the node, you need
* [.NET Core 2.1](https://www.microsoft.com/net/download/core)
* [Git](https://git-scm.com/)

## Build instructions

### Get the repository and its dependencies

```
git clone https://github.com/stratisproject/StratisBitcoinFullNode.git  
cd StratisBitcoinFullNode/src
```

### Build and run the code
With this node, you can connect to either the Stratis network or the Bitcoin network, either on MainNet or TestNet.
So you have 4 options:

1. To run a <b>Stratis</b> node on <b>MainNet</b>, do
```
cd Stratis.StratisD
dotnet run
```  

2. To run a <b>Stratis</b>  node on <b>TestNet</b>, do
```
cd Stratis.StratisD
dotnet run -testnet
```  

3. To run a <b>Bitcoin</b> node on <b>MainNet</b>, do
```
cd Stratis.BitcoinD
dotnet run
```  

4. To run a <b>Bitcoin</b> node on <b>TestNet</b>, do
```
cd Stratis.BitcoinD
dotnet run -testnet
```  

### Advanced options

You can get a list of command line arguments to pass to the node with the -help command line argument. For example:
```
cd Stratis.StratisD
dotnet run -help
```  

### Script

# Getting started - Building and running a x42 Full Node 

---------------

## Supported Platforms

* <b>Windows</b> - works from Windows 7 and later, on both x86 and x64 architecture. Most of the development and testing is happening here.
* <b>Linux</b> - works and Ubuntu 14.04 and later (x64). It's been known to run on some other distros so your mileage may vary.
* <b>MacOS</b> - works from OSX 10.12 and later. 

## Prerequisites

To install and run the node, you need
* [.NET Core 2.1](https://www.microsoft.com/net/download/core)
* [Git](https://git-scm.com/)

## Build instructions

### Get the repository and its dependencies

```
git clone https://github.com/x42protocol/X42-FullNode.git  
cd X42-FullNode

```

### Build and run the code
With this node, you can connect to either the Stratis network or the Bitcoin network, either on MainNet or TestNet.
So you have 4 options:

1. To run a <b>x42</b> node on <b>MainNet</b>, do
```
cd x42.
dotnet run
```  

2. To run a <b>x42</b>  node on <b>TestNet</b>, do
```
cd src\x42.x42D
dotnet run -testnet
```  

### Advanced options

You can get a list of command line arguments to pass to the node with the -help command line argument. For example:
```
cd src\x42.x42D
dotnet run -help
```  

Swagger Endpoints
-------------------

Once the node is running, a Swagger interface (web UI for testing an API) is available.

* For x42: http://localhost:42220/swagger/index.html
* For x42 Testnet: http://localhost:42221/swagger/index.html
