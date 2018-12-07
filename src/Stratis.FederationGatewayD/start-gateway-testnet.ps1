#############################
#    UPDATE THESE VALUES    #
#############################
$git_repos_path = "C:\Users\matthieu\source\repos"
$root_datadir = "C:\Users\matthieu\AppData\Roaming\StratisNode"
$path_to_federationgatewayd = "$git_repos_path\FederatedSidechains\src\Stratis.FederationGatewayD"

# please add the path to the federationKey.dat file generated when using federation setup tool.
$path_to_mining_key_dat_file = "path to the federationKey.dat file"

$multisig_public_key = "03b824a9500f17c9fe7a4e3bb660e38b97b66ed3c78749146f2f31c06569cf905c"
$mining_public_key = "0248de019680c6f18e434547c8c9d48965b656b8e5e70c5a5564cfb1270db79a11"

#keep this short
$nickname = "matt"

$mainchain_federationips = "127.0.0.1:26178,127.0.0.2:26178,127.0.0.3:26178,127.0.0.4:26178,127.0.0.5:26178"
$sidechain_federationips = "127.0.0.1:26179,127.0.0.2:26179,127.0.0.3:26179,127.0.0.4:26179,127.0.0.5:26179"

######################################
#    UPDATE THIS BUT DO NOT SHARE    #
######################################
$multisig_mnemonic = "please change that to the keywords generated when using federation setup tool"
# enter a password - used to protect your multisig wallet
$multisig_password = "enter a password"
# enter a different password - used to protect the wallet where poa rewards are sent
$mining_wallet_password = "enter a password"


#######################################
#    THE ACTUAL SCRIPT BEGINS HERE    #
#######################################

# Create the folders in case they don't exist.
New-Item -ItemType directory -Force -Path $root_datadir
New-Item -ItemType directory -Force -Path $root_datadir\gateway\stratis\StratisTest
New-Item -ItemType directory -Force -Path $root_datadir\gateway\fedpeg\FederatedPegTest


# Copy the blockchain data from a current, ideally up-to-date, Stratis Testnet folder.
If ((Test-Path $env:APPDATA\StratisNode\stratis\StratisTest) -And -Not (Test-Path $root_datadir\gateway\stratis\StratisTest\blocks)) {
    $destination = "$root_datadir\gateway\stratis\StratisTest"
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\blocks -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\chain -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\coinview -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\finalizedBlock -Recurse -Destination $destination
    Copy-Item $env:APPDATA\StratisNode\stratis\StratisTest\provenheaders -Recurse -Destination $destination
}

Copy-Item $path_to_mining_key_dat_file -Destination $root_datadir\gateway\fedpeg\FederatedPegTest\

# FEDERATION DETAILS
# Redeem script: 3 03eaec65a70a9164579dcf0ab9d66a821eeb1a597412aa7d28c48d7bb708deebc3 026b7b9092828f3bf9e73995bfa3547c3bcd3814f8101fac626b8349d9a6f0e534 0396f7825142a906191cf394c3b4f2fd66e1244f850eb77aff3923ef125c234ffa 03b824a9500f17c9fe7a4e3bb660e38b97b66ed3c78749146f2f31c06569cf905c 0319a589292010a61ab6da4926f1a91be7cd3791e81e5a71cd7beac157c55ff9f4 5 OP_CHECKMULTISIG
# Sidechan P2SH: OP_HASH160 812231abd79e116f5c6ff7455a047e6bccd480f7 OP_EQUAL
# Sidechain Multisig address: pHKNLJ2eoeR8owR4hMpvzXTU5zF7BG4ALC
# Mainchain P2SH: OP_HASH160 812231abd79e116f5c6ff7455a047e6bccd480f7 OP_EQUAL
# Mainchain Multisig address: 2N5227saQtu2MRBRudW97JRxcoqbdJ9UPtA

$redeemscript = "3 03eaec65a70a9164579dcf0ab9d66a821eeb1a597412aa7d28c48d7bb708deebc3 026b7b9092828f3bf9e73995bfa3547c3bcd3814f8101fac626b8349d9a6f0e534 0396f7825142a906191cf394c3b4f2fd66e1244f850eb77aff3923ef125c234ffa 03b824a9500f17c9fe7a4e3bb660e38b97b66ed3c78749146f2f31c06569cf905c 0319a589292010a61ab6da4926f1a91be7cd3791e81e5a71cd7beac157c55ff9f4 5 OP_CHECKMULTISIG"

# The interval between starting the networks run, in seconds.
$interval_time = 5
$long_interval_time = 10

$agent_prefix = $nickname + "-" + $mining_public_key.Substring(0,5)

cd $path_to_federationgatewayd
# Federation member main and side
Write-Host "Starting mainchain gateway node"
start-process cmd -ArgumentList "/k color 0E && dotnet run --no-build -mainchain -testnet -agentprefix=$agent_prefix -datadir=$root_datadir\gateway -port=26178 -apiport=38221 -counterchainapiport=38222 -federationips=$mainchain_federationips -redeemscript=""$redeemscript"" -publickey=$multisig_public_key -mincoinmaturity=1 -mindepositconfirmations=1 -addnode=13.70.81.5 -addnode=52.151.76.252 -whitelist=52.151.76.252 -gateway=1"
timeout $long_interval_time
Write-Host "Starting sidechain gateway node"
start-process cmd -ArgumentList "/k color 0E && dotnet run --no-build -sidechain -testnet -agentprefix=$agent_prefix -datadir=$root_datadir\gateway -port=26179 -apiport=38222 -counterchainapiport=38221 -federationips=$sidechain_federationips -redeemscript=""$redeemscript"" -publickey=$multisig_public_key -mincoinmaturity=1 -mindepositconfirmations=1 -txindex=1"
timeout $long_interval_time


######### API Queries to enable federation wallets ###########
# mainchain
Write-Host "Enabling multisig wallet on main chain"
$params = @{ "mnemonic" = $multisig_mnemonic; "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38221/api/FederationWallet/import-key -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time
$params = @{ "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38221/api/FederationWallet/enable-federation -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time

# sidechain
Write-Host "Enabling multisig wallet on side chain"
$params = @{ "mnemonic" = $multisig_mnemonic; "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38222/api/FederationWallet/import-key -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time
$params = @{ "password" = $multisig_password }
Invoke-WebRequest -Uri http://localhost:38222/api/FederationWallet/enable-federation -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
timeout $interval_time

# create POA wallet if needed
$params = @{ "name" = "poa-rewards"; "password" = $mining_wallet_password }
Try{
    Write-Host "Loading wallet for poa-rewards"
    Invoke-WebRequest -Uri http://localhost:38222/api/Wallet/load -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
}
Catch {

    $ErrorMessage = $_.Exception.Message
    If ($ErrorMessage.Contains("404")) 
    {
        Write-Host "Creating wallet for poa-rewards"
        $params = @{ "name" = "poa-rewards"; "password" = $mining_wallet_password; "passphrase" = $mining_wallet_password; "mnemonic" = $multisig_mnemonic;  }
        Invoke-WebRequest -Uri http://localhost:38222/api/Wallet/create -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"
    }
}
