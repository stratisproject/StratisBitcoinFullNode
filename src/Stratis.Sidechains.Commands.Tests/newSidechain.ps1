Import-Module .\Stratis.Sidechains.Features.BlockchainGeneration.dll
Import-Module .\Stratis.Sidechains.Commands.dll

$Time = 1510170036
$Nonce = 2500036
$MessageStart = 7846846
$AddressPrefix = 61
$Port = 36000
$RpcPort = 36100
$ApiPort = 36200
$CoinSymbol = "EGA1"
$GenesisHashHex = $null

$mainnet =  new-object Stratis.Sidechains.Features.BlockchainGeneration.NetworkInfoRequest(
        $Time, $Nonce, $MessageStart, $AddressPrefix,
        $Port, $RpcPort, $ApiPort, 
        $CoinSymbol, $GenesisHashHex)

$Time = 1510170037
$Nonce = 2500037
$MessageStart = 7846847
$AddressPrefix = 120
$Port = 37000
$RpcPort = 37100
$ApiPort = 37200
$CoinSymbol = "EGA2"
$GenesisHashHex = $null
$testnet =  new-object Stratis.Sidechains.Features.BlockchainGeneration.NetworkInfoRequest(
        $Time, $Nonce, $MessageStart, $AddressPrefix,
        $Port, $RpcPort, $ApiPort, 
        $CoinSymbol, $GenesisHashHex)

$Time = 1510170038
$Nonce = 2500038
$MessageStart = 7846848
$AddressPrefix = 63
$Port = 38000
$RpcPort = 38100
$ApiPort = 38200
$CoinSymbol = "EGA3"
$GenesisHashHex = $null
$regtest = new-object Stratis.Sidechains.Features.BlockchainGeneration.NetworkInfoRequest(
        $Time, $Nonce, $MessageStart, $AddressPrefix,
        $Port, $RpcPort, $ApiPort, 
        $CoinSymbol, $GenesisHashHex)

$ChainName = "anotherEnigma"
$CoinName = "enigmaCoin"
$CoinType = 1234
new-sidechain $ChainName $CoinName $CoinType $mainnet $testnet $regtest
