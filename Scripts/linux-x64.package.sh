#!/bin/bash

dotnet_runtime="linux-x64"
warp_runtime="linux-x64"
configuration="release"
git_commit=$(git log --format=%h --abbrev=7 -n 1)
publish_directory="../src/Stratis.StratisD/bin/${configuration}/netcoreapp2.1/${dotnet_runtime}/publish"
download_directory="/tmp"
warp="${warp_runtime}.warp-packer"
project_path="../src/Stratis.StratisD/Stratis.StratisD.csproj"

echo warp is ${warp}
echo "Download directory is:" $download_directory
echo "Publish directory is: " $publish_directory
echo "Download file is " ${download_directory}/${warp}
echo "Current directory is:" $PWD 
echo "Git commit to build:" $git_commit

echo "Downloading warp..."
curl -L -o ${download_directory}/${warp} "https://github.com/stratisproject/warp/releases/download/v0.2.1/${warp}"

if [ -f "${download_directory}/${warp}" ]; then
   echo "Warp packer downloaded succesfully."
else
   echo "Warp packer didn't download successfully."
fi

echo "Building the full node..."
dotnet --info
dotnet publish $project_path -c $configuration -v m -r $dotnet_runtime 

echo "List of files to package:" 
ls $publish_directory

echo "Packaging the daemon..."
chmod +x "${download_directory}/${warp}"
"${download_directory}/./${warp}" --arch $warp_runtime --input_dir $publish_directory --exec Stratis.StratisD --output ${publish_directory}/Stratis-$git_commit

echo "Done."
