$configuration="release"
$runtime="win-x64"
$git_commit=(git log --format=%h --abbrev=7 -n 1)
$publish_directory="..\src\Stratis.StratisD\bin\$configuration\netcoreapp2.1\$runtime\publish"
$download_directory=$env:temp
$warp="$download_directory\windows-x64.warp-packer.exe"
$project_path="..\src\Stratis.StratisD\Stratis.StratisD.csproj"

Write-Host "Download directory is $download_directory" -foregroundcolor "Magenta"
Write-Host "Current directory is $PWD" -foregroundcolor "Magenta"
Write-Host "Git commit to build: $git_commit" -foregroundcolor "Magenta"

Write-Host "Downloading warp..." -foregroundcolor "Magenta"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest https://github.com/stratisproject/warp/releases/download/v0.2.1/windows-x64.warp-packer.exe -O $download_directory\windows-x64.warp-packer.exe

If(Get-ChildItem $warp)
{
    Write-Host "Warp downloaded succesfully." -foregroundcolor "Magenta"

    $size=((Get-Item "$warp").Length)
    Write-Host "Size is $size" -foregroundcolor "Magenta"
}

Write-Host "Building the full node..." -foregroundcolor "Magenta"
dotnet --info
dotnet publish $project_path -c $configuration -v m -r $runtime 

Write-Host "List of files to package:" -foregroundcolor "Magenta"
Get-ChildItem -Path $publish_directory

Write-Host "Packaging the daemon..." -foregroundcolor "Magenta"
& $warp --arch windows-x64 --input_dir $publish_directory --exec Stratis.StratisD.exe --output $publish_directory\Stratis-$git_commit.exe

Write-Host "Done." -foregroundcolor "green"
Read-Host "Press ENTER"
