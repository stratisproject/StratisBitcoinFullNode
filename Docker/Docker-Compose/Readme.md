# Start the network
To start the network:  
`docker-compose -f EdgeNode.yml up`

# Stop the network
The network coud be stopped by pressing Ctrl+c or by executing the following command:  
`docker-compose -f EdgeNode.yml up`

# Update
In order to update to the latest version update the image with the following command:  
`docker pull stratisgroupltd/blockchaincovid19:latest`

# Clean the state
Docker compose retains state of the network. In order to reset and start all over again execute the following command:  
`docker-compose -f EdgeNode.yml rm`
