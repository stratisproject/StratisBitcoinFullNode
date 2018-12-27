| Windows | MacOs | Ubuntu64
| :---- | :------ | :---- |
| [![Build Status](https://dev.azure.com/StratisProject/FederatedSidechains/_apis/build/status/FederatedSidechains-.NET-HostedWindowsContainer-CI?branchName=master)](https://dev.azure.com/StratisProject/FederatedSidechains/_build?definitionId=1) | [![Build Status](https://dev.azure.com/StratisProject/FederatedSidechains/_apis/build/status/FederatedSidechains-.NET-HostedmacOS-CI?branchName=master)](https://dev.azure.com/StratisProject/FederatedSidechains/_build?definitionId=3) | [![Build Status](https://dev.azure.com/StratisProject/FederatedSidechains/_apis/build/status/FederatedSidechains-.NET-HostedUbuntu1604-CI?branchName=master)](https://dev.azure.com/StratisProject/FederatedSidechains/_build?definitionId=2)


Stratis Federated Sidechains
============================
https://stratisplatform.com


# Get Started with Cirrus
The below steps will guide you through the following.

 - Clone the FederatedSidechains Repository
 - Run a node on the Stratis Sidechain (Cirrus)
 - Create a Wallet via the API
 - Retreive an Address via the API
 - How to get funds
 
 For simplicity, PowerShell commands have been provided for each section.

 ## Step 1 - Clone FederatedSidechains Repository

To begin, we first need to define the location that the Stratis FederatedSidechains repository will be cloned to, this can be done by executing the below script-block. The example below will utilize the logged-on user's desktop directory.

    $CloneDirectory = "$env:USERPROFILE\Desktop\FederatedSidechains"

We then need to define the repository that you will clone.

    $RepositoryURL = "https://github.com/stratisproject/FederatedSidechains.git"

Now we can clone the repository using Git. 

    Start-Process "git.exe" -ArgumentList "clone $RepositoryURL $CloneDirectory"



## Step 2 - Run the Sidechain Node

To run a Cirrus Node we simply need to run the Stratis.SideChainD project. This can be achieved by running the below PowerShell script-block.

    Set-Location "$CloneDirectory\src\Stratis.SidechainD"
    Start-Process "dotnet.exe" -ArgumentList "run"

In addition, we can run the below PowerShell script-block that will wait for the Sidechain Node API to become available and then present it to you in Internet Explorer.

    While (!(Get-NetTCPConnection -LocalPort "38225" -ErrorAction SilentlyContinue)) {
        Write-Host "Waiting for node to become available..." -ForegroundColor Yellow
        Start-Sleep 10}
        Start-Process "iexplore.exe" -ArgumentList "http://localhost:38225/swagger/index.html"
   

## Step 3 - Create a Wallet

Now you are running a node, you will now need to create a wallet that will be used to store funds on the Cirrus Sidechain. This can be done interactively via the Swagger API that we opened previously, alternatively, this can be done via PowerShell.

The below PowerShell script-block with generate your unique mnemonic words.

    $Mnemonic = Invoke-WebRequest -Uri "http://localhost:38225/api/Wallet/mnemonic?language=english&wordCount=12" | Select-Object -ExpandProperty Content
    $Mnemonic = $Mnemonic -replace '["]',''
    $Mnemonic
    
**Important: Please be sure to keep note of your mnemonic words**

Now we have a set of unique mnemonic words, we can create a wallet. 

    $WalletName = Read-Host -Prompt "What do you want to call the wallet?"
    $WalletPassphrase = Read-Host -Prompt "Please enter a Passphrase to secure the private key"
    $WalletPassword = Read-Host -Prompt "Please enter a Password to secure the wallet"
    $Params = @{"mnemonic" = $Mnemonic; "password" = $WalletPassword; "passphrase" = $WalletPassword; "name" = "$WalletName"}
    Invoke-WebRequest -Uri http://localhost:38225/api/Wallet/create -Method post -Body ($Params|ConvertTo-Json) -ContentType "application/json"

We now have now created a wallet. 

**Important: Please be sure to keep note of your mnemonic words, passphrase and password. These will be needed to recover a wallet in the event of a disaster.**

## Step 4 - Obtain an Address

In order to receive funds in your newly created wallet, you will need to obtain an address that is unique to your wallet. You can do this by executing the below PowerShell script-block.

    $Address = Invoke-WebRequest -Uri "http://localhost:38225/api/Wallet/unusedaddress?WalletName=$WalletName&AccountName=account%200" | Select-Object -ExpandProperty Content
    $Address = $Address -replace '["]',''
    $Address

## Step 5 - How to get funds?

The token issued on the Cirrus Sidechain is CRS. These are pegged to the Stratis Mainnet Chain and are valued at a 1:1 ratio. i.e. 1 STRAT is equal to 1 CRS. 

Transferring STRAT to the Cirrus Sidechain can be achieved by further interacting with the API, however, it will be introduced as a feature in an upcoming version of Stratis Core, allowing for seamless transfers from one chain to another within the UI of Stratis Core. 

For the Cirrus Sidechain, Stratis has set aside an amount of CRS that it will distribute to anyone wanting to deploy Smart Contracts on the Cirrus Sidechain. CRS tokens hold a value, as they are pegged to the STRAT token, as a result, CRS will be distributed at the discretion of Stratis.

To get your hands on some CRS and start deploying Smart Contracts in C#, head over to our Discord channel where there will be plenty of people whom are able to send some CRS to your newly created Cirrus Wallet.

[Discord](https://discordapp.com/invite/9tDyfZs)

For more information on how to deploy a Smart Contract in C#, head over to the Stratis Academy.

[Stratis Academy - Smart Contracts in C#](https://academy.stratisplatform.com/SmartContracts/smart-contracts-introduction.html)
