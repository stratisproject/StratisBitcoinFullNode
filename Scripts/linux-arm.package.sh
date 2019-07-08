#!/bin/bash
NONE='\033[00m'
RED='\033[01;31m'
GREEN='\033[01;32m'
YELLOW='\033[01;33m'
PURPLE='\033[01;35m'
CYAN='\033[01;36m'
WHITE='\033[01;37m'
BOLD='\033[1m'
UNDERLINE='\033[4m'

runtime="linux-arm"
configuration="release"
git_commit=$(git log --format=%h --abbrev=7 -n 1)
publish_directory="/tmp/City/Release/Publish"
release_directory="/tmp/City/Release"
project_path="../src/City.Chain/City.Chain.csproj"

function check_root {
if [ "$(id -u)" != "0" ]; then
    echo "Sorry, this script needs to be run as root. Do \"sudo su root\" and then re-run this script"
    exit 1
fi
}

function DisplayParameters {
echo "Publish directory is:" $publish_directory
echo "Project file is     :" ${project_path}
echo "Current directory is:" $PWD 
echo "Git commit to build :" $git_commit
}

function compileWallet {
    echo -e "* Compiling wallet. Please wait, this might take a while to complete..."
    #dotnet --info  
    mkdir -p $publish_directory
    dotnet publish $project_path -c $configuration -v m -r $runtime --no-dependencies -o $publish_directory #--self-contained
    cd $publish_directory
    tar -cvf $release_directory/City.Chain-$runtime-$git_commit.tar *
    rm -rf $publish_directory
    echo -e "${NONE}${GREEN}* Done${NONE}"
}


clear
echo && echo
echo -e "${YELLOW}        .d8888b.  d8b 888                   .d8888b.  888               d8b${NONE}"          
echo -e "${YELLOW}       d88P  Y88b Y8P 888                  d88P  Y88b 888               Y8P${NONE}"         
echo -e "${YELLOW}       888    888     888                  888    888 888${NONE}"                            
echo -e "${YELLOW}       888        888 888888 888  888      888        88888b.   8888b.  888 88888b.${NONE}"  
echo -e "${YELLOW}       888        888 888    888  888      888        888 *88b     \"88b 888 888 \"88b${NONE}" 
echo -e "${YELLOW}       888    888 888 888    888  888      888    888 888  888 .d888888 888 888  888${NONE}" 
echo -e "${YELLOW}       Y88b  d88P 888 Y88b.  Y88b 888      Y88b  d88P 888  888 888  888 888 888  888${NONE}" 
echo -e "${YELLOW}        \"Y8888P\"  888  \"Y888  \"Y88888       \"Y8888P\"  888  888 \"Y888888 888 888  888${NONE}" 
echo -e "${YELLOW}                                  888${NONE}"                                                
echo -e "${YELLOW}                             Y8b d88P${NONE}"                                                
echo -e "${YELLOW}                              \"Y88P\"${NONE}"                                                 
echo -e ${YELLOW}
echo -e ${YELLOW}
echo -e "${PURPLE}**********************************************************************${NONE}"
echo -e "${PURPLE}*    ${NONE}This script will compile the full node for ${runtime}.               *${NONE}"
echo -e "${PURPLE}**********************************************************************${NONE}"
echo -e "${BOLD}"
read -p "Please run this script as the root user. Do you want to compile full node for ${runtime} (y/n)?" response
echo

echo -e "${NONE}"

if [[ "$response" =~ ^([yY][eE][sS]|[yY])+$ ]]; then

    check_root
    DisplayParameters
    compileWallet
	
echo
echo -e "${GREEN} Installation complete. ${NONE}"
echo -e "${GREEN} thecrypt0hunter(2018)${NONE}"
else

   echo && echo -e "${RED} Installation cancelled! ${NONE}" && echo
fi

