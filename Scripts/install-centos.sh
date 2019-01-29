# Manual steps to run City Chain daemon on CentOS 7.x, running in interactive mode (not installed as service/daemon).

mkdir citychain
cd citychain

sudo rpm -Uvh https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
sudo yum update -y
sudo yum install wget -y
sudo yum install aspnetcore-runtime-2.1 -y

# Opens the P2P protocol port, this is not required, but will make the node into a public node.
firewall-cmd --zone=public --add-port=4333/tcp --permanent

# Opens the RPC protocol port, this is required for RPC calls outside of the machine.
firewall-cmd --zone=public --add-port=4334/tcp --permanent

# Opens the API port, this is required to generate multiple wallet addresses. API documentation: http://localhost:4335/swagger/index.html
firewall-cmd --zone=public --add-port=4335/tcp --permanent

# Reload the firewall. Also make sure that the zone is correct, it might be dmz and not public. Run this command to see firewall zones: firewall-cmd --get-active-zones
firewall-cmd --reload

wget -q https://github.com/CityChainFoundation/city-chain/releases/download/v1.0.16/City.Chain-1.0.16-linux-x64.tar.gz
tar zxf City.Chain-1.0.16-linux-x64.tar.gz
chmod +x City.Chain
./City.Chain -server=1 -txindex=1 -walletnotify=curl -X POST -d txid=%s http://localhost/api -apiuri=http://0.0.0.0:4335 -rpcallowip=0.0.0.0 -rpcport=4334 -rpcuser=rpcuser -rpcpassword=rpcpassword -defaultwallet=1 -defaultwalletpassword=default

# Wait for connections to network nodes and blockchain sync will begin.
