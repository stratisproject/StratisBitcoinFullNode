
###############################
#    UPDATE THESE 5 VALUES    #
###############################
$root_datadir = "D:\federation" 
$path_to_federationgatewayd = "D:\GitHub\FederatedSidechains\src\Stratis.FederationGatewayD"
$path_to_sidechaind = "D:\GitHub\FederatedSidechains\src\Stratis.SidechainD"
$path_to_stratisd = "D:\GitHub\StratisBitcoinFullNode\src\Stratis.StratisD"
$path_to_stratis_wallet_with_funds = "C:\Users\jerem\AppData\Roaming\StratisNode\stratis\StratisTest\myRecoveredWallet.wallet.json"

New-Item -ItemType directory -Force -Path $root_datadir
New-Item -ItemType directory -Force -Path $root_datadir\gateway1\stratis\StratisTest
New-Item -ItemType directory -Force -Path $root_datadir\gateway2\stratis\StratisTest
New-Item -ItemType directory -Force -Path $root_datadir\gateway3\stratis\StratisTest
New-Item -ItemType directory -Force -Path $root_datadir\MainchainUser\stratis\StratisTest
New-Item -ItemType directory -Force -Path $root_datadir\MiningNode
New-Item -ItemType directory -Force -Path $root_datadir\SidechainUserNode

If ((Test-Path $env:APPDATA\StratisNode\stratis\StratisTest) -And -Not (Test-Path $root_datadir\gateway1\stratis\StratisTest\blocks)) 
{
	$destinations = "$root_datadir\gateway1\stratis\StratisTest","$root_datadir\gateway2\stratis\StratisTest","$root_datadir\gateway3\stratis\StratisTest","$root_datadir\MainchainUser\stratis\StratisTest"
	$destinations | % {Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\blocks -Recurse -Destination $_}
	$destinations | % {Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\chain -Recurse -Destination $_}
	$destinations | % {Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\coinview -Recurse -Destination $_}
    Copy-Item -Path $path_to_stratis_wallet_with_funds -Destination $root_datadir\MainchainUser\stratis\StratisTest
}

# FEDERATION DETAILS
# Member1 mnemonic: dilemma sponsor simple sheriff people own what table style typical grain isolate
# Member1 public key: 02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335
# Member2 mnemonic: lens swing super peanut magnet liquid clump wolf nurse critic song dog
# Member2 public key: 020eff5b198302436e61e1454ae107ec65318af4cc61d492a91c1cf943b3431f3a
# Member3 mnemonic: ice scrub crawl goose bus affair end tail teach motion lion ostrich
# Member3 public key: 0229125011a192ad7cbcb966a2bf77b17c4dda5116560fb4fb16adccab005638be
# Redeem script: 2 02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335 020eff5b198302436e61e1454ae107ec65318af4cc61d492a91c1cf943b3431f3a 0229125011a192ad7cbcb966a2bf77b17c4dda5116560fb4fb16adccab005638be 3 OP_CHECKMULTISIG
# Sidechan P2SH: OP_HASH160 edd85882c9b64ee2511f6135c670033662bed7af OP_EQUAL
# Sidechain Multisig address: pTEBX8oNk6GoPufm755yuFtbBgEPmjPvdK
# Mainchain P2SH: OP_HASH160 edd85882c9b64ee2511f6135c670033662bed7af OP_EQUAL
# Mainchain Multisig address: 2NEvqJiM8qLt219gc3DQADAPjuXauwiFjRm


$mainchain_federationips="127.0.0.1:36011,127.0.0.1:36021,127.0.0.1:36031"
$sidechain_federationips="127.0.0.1:36012,127.0.0.1:36022,127.0.0.1:36032"
$redeemscript="2 02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335 020eff5b198302436e61e1454ae107ec65318af4cc61d492a91c1cf943b3431f3a 0229125011a192ad7cbcb966a2bf77b17c4dda5116560fb4fb16adccab005638be 3 OP_CHECKMULTISIG"
$publickey="034b191e3b3107b71d1373e840c5bf23098b55a355ca959b968993f5dec699fc38"
$sidechain_multisig_address="pTEBX8oNk6GoPufm755yuFtbBgEPmjPvdK"
$gateway1_public_key="02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335"
$gateway2_public_key="020eff5b198302436e61e1454ae107ec65318af4cc61d492a91c1cf943b3431f3a"
$gateway3_public_key="0229125011a192ad7cbcb966a2bf77b17c4dda5116560fb4fb16adccab005638be"

$color_gateway1 = "0E" # light yellow on black
$color_gateway2 = "0A" # light green on black
$color_gateway3 = "09" # light blue on black
$color_miner    = "0C" # light red on black
$color_wallets  = "0D" # light purple on black

# The interval between starting the networks run, in seconds.
$interval_time = 5
$long_interval_time = 15 


cd $path_to_federationgatewayd

# Federation member 1 main and side
start-process cmd -ArgumentList "/k color $color_gateway1 && dotnet run -mainchain -agentprefix=fed1main -datadir=$root_datadir\gateway1 -port=36011 -apiport=38011 -counterchainapiport=38012 -federationips=$mainchain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway1_public_key"
timeout $long_interval_time
start-process cmd -ArgumentList "/k color $color_gateway1 && dotnet run -sidechain -agentprefix=fed1side -datadir=$root_datadir\gateway1 mine=1 mineaddress=$sidechain_multisig_address -port=36012 -apiport=38012 -counterchainapiport=38011 -txindex=1 -federationips=$sidechain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway1_public_key"
timeout $interval_time


# Federation member 2 main and side
start-process cmd -ArgumentList "/k color $color_gateway2 && dotnet run -mainchain -agentprefix=fed2main -datadir=$root_datadir\gateway2 -port=36021 -apiport=38021 -counterchainapiport=38022 -federationips=$mainchain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway2_public_key"
timeout $long_interval_time
start-process cmd -ArgumentList "/k color $color_gateway2 && dotnet run -sidechain -agentprefix=fed2side -datadir=$root_datadir\gateway2 mine=1 mineaddress=$sidechain_multisig_address -port=36022 -apiport=38022 -counterchainapiport=38021 -txindex=1 -federationips=$sidechain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway2_public_key"
timeout $interval_time


# Federation member 3 main and side
start-process cmd -ArgumentList "/k color $color_gateway3 && dotnet run -mainchain -agentprefix=fed3main -datadir=$root_datadir\gateway3 -port=36031 -apiport=38031 -counterchainapiport=38032 -federationips=$mainchain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway3_public_key"
timeout $long_interval_time
start-process cmd -ArgumentList "/k color $color_gateway3 && dotnet run -sidechain -agentprefix=fed3side -datadir=$root_datadir\gateway3 mine=1 mineaddress=$sidechain_multisig_address -port=36032 -apiport=38032 -counterchainapiport=38031 -txindex=1 -federationips=$sidechain_federationips -redeemscript=""$redeemscript"" -publickey=$gateway3_public_key"
timeout $interval_time


cd $path_to_stratisd

# MainchainUser
start-process cmd -ArgumentList "/k color $color_wallets && dotnet run -testnet -port=36178 -apiport=40000 -agentprefix=mainuser -datadir=$root_datadir\MainchainUser"
timeout $interval_time


cd $path_to_sidechaind

# SidechainUserNode
start-process cmd -ArgumentList "/k color $color_wallets && dotnet run -port=26179 -apiport=39001 -agentprefix=sideuser -datadir=$root_datadir\SidechainUserNode agentprefix=sc_user -addnode=127.0.0.1:36012 -addnode=127.0.0.1:36022 -addnode=127.0.0.1:36032"
timeout $interval_time
