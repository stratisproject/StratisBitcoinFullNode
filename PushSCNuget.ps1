$packageNames = @("Stratis.SmartContracts.RuntimeObserver", "Stratis.SmartContracts.Core", "Stratis.SmartContracts.CLR", "Stratis.SmartContracts.CLR.Validation", "Stratis.SmartContracts.Standards", "Stratis.Bitcoin.Features.SmartContracts")

# A little gross to have to enter src/ and then go back after, but this is where the file is atm 
cd "src"

foreach ($packageName in $packageNames){
	cd $packageName
	rm "bin\debug\" -Recurse -Force -ErrorAction Ignore
	dotnet pack --configuration Debug --include-source --include-symbols 
	dotnet nuget push "bin\Debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"
	cd ..
}

cd ..
