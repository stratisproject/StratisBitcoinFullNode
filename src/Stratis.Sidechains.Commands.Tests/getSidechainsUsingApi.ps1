Import-Module .\NBitcoin.dll
Import-Module .\Stratis.Sidechains.Commands.dll

$ApiUrl = "http://localhost:[@API_PORT@]/api"
get-sidechainsusingapi $null $ApiUrl 