# City Chain: Docker Setup

Please first refer to the [README.md](README.md) for introduction to the Docker setup, then use this
file for additional setup.

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

