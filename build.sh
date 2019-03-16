#!/bin/bash

if [ -z "${TRAVIS_OS_NAME+x}" ]; then
    if [ "$(uname -s)" = "Darwin" ]; then
	    OS_NAME="osx"
    else
	    OS_NAME="linux"
    fi
else
    OS_NAME="${TRAVIS_OS_NAME}"
fi

function DEBUG {
    # Output dotnet information.
    dotnet --info
}

function INSTALL_APT_PACKAGES {
    echo "Installing dependency packages using aptitude."
        if [ -z "${TRAVIS+x}" ]; then
	# if not on travis, its nice to see progress
	    QUIET=""
    else
	    QUIET="-qq"
    fi
    # get the required OS packages
    sudo apt-get ${QUIET} update
    sudo apt-get ${QUIET} install -y --no-install-recommends \
	  apt-transport-https dotnet-sdk-2.1
}

function brew_if_not_installed() {
    if ! brew ls | grep $1 --quiet; then
	    brew install $1
    fi
}

function INSTALL_BREW_PACKAGES {
    echo "Installing dependency packages using homebrew."
    brew tap caskroom/cask
    brew update > /dev/null
    
    brew_if_not_installed dotnet
}

function REGISTER_MS_KEY {
    wget -q https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
}

function INSTALL_DEPENDENCIES {
    echo "Finding Correct Package Manager."
    if [ "${OS_NAME}" = "osx" ]; then
        INSTALL_BREW_PACKAGES
    else
        REGISTER_MS_KEY
	    INSTALL_APT_PACKAGES
    fi
}

function BUILD {
    # Detect Operating And Install Dependency Packages.
    INSTALL_DEPENDENCIES

    echo "Restoring X42 Dependencies."
    dotnet restore 

    echo "Building X42 & Dependencies."
    dotnet build -c Release ${path} -v m
}

if [ "$1" == "debug" ]; then
    DEBUG
fi

BUILD