# City Chain: Docker Setup

Please first refer to the [README.md](README.md) for introduction to the Docker setup, then use this
file for additional setup.


You can build (and run) docker images for City Chain from either distributed releases, or from source code.

The 4 different options are:

* City.Chain
* City.Chain-src
* City.Chain-TestNet
* City.Chain-TestNet-src

The *-src folders will download latest source code, compile and run. It will generate larger docker images, which
includes the .NET Core SDK. The *-src option is primarily for developers, or anyone who want to run on an 
architecture which is not available in release distribution (e.g. ARM architecture).

## City Chain (TESTNET)

### Build with name (tag) "citychaintest"

```
docker build . -t citychaintest
```

### Create volume and run with persistent blockchain database

```
docker volume create citychaintest 
docker run -it -p 24333:24333 -p 24334:24334 -p 24335:24335 -p 24336:24336 --mount source=citychaintest,target=/root/.citychain citychaintest
```

## City Chain (MAINNET)


### Build with name (tag) "citychain"

```
docker build . -t citychain
```

### Create volume and run with persistent blockchain database

```
docker volume create citychain
docker run -it -p 4333:4333 -p 4334:4334 -p 4335:4335 -p 2336:4336 --mount source=citychain,target=/root/.citychain citychain
```

# Extras

## Run multiple instances as daemons

docker run -d IMAGE
docker run -d IMAGE

## List running instances

docker ps

## Run a full local network with port forward:

docker run -d -p 24333:24333 -p 24334:24334 -p 24335:24335 -p 24336:24336 IMAGE
docker run -d -p 25333:24333 -p 25334:24334 -p 25335:24335 -p 25336:24336 IMAGE
docker run -d -p 26333:24333 -p 26334:24334 -p 26335:24335 -p 26336:24336 IMAGE
docker run -d -p 27333:24333 -p 27334:24334 -p 27335:24335 -p 27336:24336 IMAGE

## Clean (prune) all docker data

Use this command with care, it will clean everything on your local docker environment:

```
docker system prune
```

https://docs.docker.com/engine/reference/commandline/system_prune/
