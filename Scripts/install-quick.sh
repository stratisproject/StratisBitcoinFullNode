#!/bin/bash
# ------------------------------------------------------------------
# City Chain Setup Script
#		  Download and deploy citychain daemon as a service
#
#		  Deploy the latest release:
#			citydeploy -i
#
#		  Deploy a particular release:
#		  citydeploy -i [VERSION]
#
#     Author: in-cred-u-lous
# ------------------------------------------------------------------

SUBJECT=citydeploy
USAGE="Usage: citydeploy -i [VERSION]"
INSTALL_DIR="$HOME/city/daemon/"

# --- Options processing -------------------------------------------
if [ $# == 0 ] ; then
	echo $USAGE
	exit 1;
fi

while getopts ":i:vh" optname
  do
	case "$optname" in
	  "i")
		VERSION=$OPTARG
		;;
	  "h")
		echo $USAGE
		exit 0;
		;;
	  "?")
		echo "Unknown option $OPTARG"
		exit 0;
		;;
	  ":")
		echo "No argument value for option $OPTARG"
		exit 0;
		;;
	  *)
		echo "Unknown error while processing options"
		exit 0;
		;;
	esac
  done

shift $(($OPTIND - 1))

# --- Lock file ---------------------------------------------------
LOCK_FILE=/tmp/$SUBJECT.lock
if [ -f "$LOCK_FILE" ]; then
   echo "Script is already running"
   exit
fi
touch $LOCK_FILE


# --- Create temp directory ---------------------------------------
TEMP_DIR=`mktemp -d`

# Check if tmp dir was created
if [[ ! "$TEMP_DIR" || ! -d "$TEMP_DIR" ]]; then
  echo "Could not create temp dir"
  exit 1
fi


# --- Cleanup function on EXIT ------------------------------------
function cleanup {
	rm -rf "$TEMP_DIR"
	rm -f "$LOCK_FILE"
}
trap cleanup EXIT


# --- Script logic ------------------------------------------------

# Get latest release
if [ "$VERSION" = "latest" ]; then 
	LATEST_RELEASE=$(curl -L -s -H 'Accept: application/json' https://github.com/CityChainFoundation/city-chain/releases/latest)

	# Releases are returned in the format {"id":3622206,"tag_name":"hello-1.0.0.11",...}, we have to extract the tag_name.
	VERSION=$(echo $LATEST_RELEASE | sed -e 's/.*"tag_name":"\([^"]*\)".*/\1/')
	VERSION=${VERSION:1}
fi;

# Build download URL
FILE="City.Chain-$VERSION-linux-x64.tar.gz"
URL="https://github.com/CityChainFoundation/city-chain/releases/download/v$VERSION/$FILE"

# Download and unpack
echo "Downloading city-chain version $VERSION..."
if wget -q $URL -P $TEMP_DIR; then
	echo "Unpacking $FILE..."
	tar zxf "$TEMP_DIR/$FILE" -C $TEMP_DIR
else
	echo "Failed to download requested City.Chain $VERSION"
fi;

# Move City.Chain daemon to install dir
cp -a "${TEMP_DIR}/." "$INSTALL_DIR"


# Create City.Chain system unit file
CONFIG="$HOME/.config/systemd/City.Chain.service"
echo "Creating system unit file: $CONFIG"
mkdir -p $(dirname "$CONFIG") && > $CONFIG

cat <<EOT >> $CONFIG
[Unit]
Description=City.Chain

[Service]
WorkingDirectory=$HOME
ExecStart=${INSTALL_DIR}City.Chain
User=$USER

[Install]
WantedBy=multi-user.target
Alias=City.Chain.service

EOT

# Configure firewall to allow incoming connections (if ufw is in use)
echo "Open port 4333 for incoming connections"
ufw allow 4333

# Start City.Chain service
echo "Start City.Chain daemon..."
#systemctl daemon-reload
systemctl enable "$CONFIG"
systemctl start City.Chain.service
systemctl status City.Chain.service

# -----------------------------------------------------------------

exit 1