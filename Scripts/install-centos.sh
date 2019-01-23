# Manual steps to run City Chain daemon on CentOS 7.x, running in interactive mode (not installed as service/daemon).

mkdir citychain
cd citychain

sudo rpm -Uvh https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
sudo yum update -y
sudo yum install wget -y
sudo yum install aspnetcore-runtime-2.1 -y

wget -q https://github.com/CityChainFoundation/city-chain/releases/download/v1.0.16/City.Chain-1.0.16-linux-x64.tar.gz
tar zxf City.Chain-1.0.16-linux-x64.tar.gz
chmod +x City.Chain
./City.Chain

# Wait for connections to network nodes and blockchain sync will begin.
