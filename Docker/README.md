# Stratis Docker Images

Here we have some basic Docker images for testing Stratis.BitcoinD and Stratis.StratisD. The images build from the full node master on GitHub. After installing Docker, you can build and run the container with the following. 

# Build the Docker container 

```
cd Stratis.StratisD
docker build . 
```

# Run the Docker container
```
docker run -it <containerId>
```

# Optional

You can optionally use volumes external to the Docker container so that the blockchain does not have to sync between tests. 

## Create the volume:

```
 docker volume create stratisbitcoin
```

### Run StratisD with a volume:
```
docker run --mount source=stratisbitcoin,target=/root/.stratisnode -it <containerId>
```

### Run BitcoinD with a volume:
```
docker run --mount source=stratisbitcoin,target=/root/.stratisbitcoin -it <containerId>
```

## Optionally forward ports from your localhost to the docker image

When running the image, add a `-p <containerPort>:<localPort>` to formward the ports:

```
docker run -p 37220:37220 -it <containerId>
```

## Force rebuild of docker images from master
```
docker build . --no-cache 
```

## Run image on the MainNet rather than the TestNet. 

Modify the Dockerfile to put the conf file in the right location and remove the "-testnet" from the run statement. 

``` 
---

COPY bitcoin.conf.docker /root/.stratisnode/bitcoin/Main/bitcoin.conf

--- 

CMD ["dotnet", "run"]

``` 

Also remove `testnet=1` from the `*.docker.conf` file.

