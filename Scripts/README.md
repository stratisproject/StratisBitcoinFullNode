# City Installer for Linux

Quick Start:

<code>
bash <( curl -L https://bit.ly/citychain-install )
</code>

## Exchange

Make sure you read the [exchange documentation](../Documentation/exchange.md) to install a wallet node for
use on exchanges.

## Linux (Precompiled Binary)

We advise installing the precompiled binaries, as these are fully tested and verified. Depending on your version and distrubtion of Linux, this might not work for you, then look at alternative scripts below.

<code>
bash <( curl -L https://bit.ly/citychain-install )
</code>

## Ubuntu

To install City Chain Node on Ubuntu 16.04 - as <code>sudo su root</code> run the following command:

<code> bash <( curl https://raw.githubusercontent.com/CityChainFoundation/city-chain/master/Scripts/install_city.sh ) </code>

## Raspberry Pi

To install a City Chain Node on a Raspberry Pi running Raspian - as <code>sudo su root</code> run the following:

<code> bash <( curl https://raw.githubusercontent.com/CityChainFoundation/city-chain/master/Scripts/install_city_RPI.sh ) </code>

## CentOS

To install a City Chain Node on CentOS (Core) - as <code>sudo su root</code> run the following:

<code>
sudo yum install curl

bash <( curl https://raw.githubusercontent.com/CityChainFoundation/city-chain/master/Scripts/install_city_CentOS.sh )
</code>

Alternative short-URL can be used, this required the -L flag for curl to make it follow redirect:

<code>
bash <( curl -L https://bit.ly/citychain-install-centos )
</code>

## Notes

If you get the error "bash: curl: command not found", run this first: <code>apt-get -y install curl</code>

## Installation log

The output log for the installation is written to the tmp folder, example: "/tmp/city_19-01-01-1553174741_output.log"