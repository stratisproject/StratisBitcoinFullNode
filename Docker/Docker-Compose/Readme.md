# Start the network
This guide is going to use **1-NodeNetwork.yml** as an example. Any of the yml files could be used to start a network made between 1 and 10 nodes.  
The yml configuration could be extended to create a network of any size.  
  
To start the network:  
`docker-compose -f 1-NodeNetwork.yml up`

# Stop the network
The network coud be stopped by pressing Ctrl+c or by executing the following command:  
`docker-compose -f 1-NodeNetwork.yml up`

# Update
In order to update to the latest version update the image with the following command:  
`docker pull stratisgroupltd/blockchaincovid19:latest`

# Clean the state
Docker compose retains state of the network. In order to reset and start all over again execute the following command:  
`docker-compose -f 1-NodeNetwork.yml rm`