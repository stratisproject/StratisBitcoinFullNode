#Create functions
function Get-IndexerStatus
{
    $Headers = @{}
    $Headers.Add("Accept","application/json")
    $AsyncLoopStats = Invoke-WebRequest -Uri http://localhost:$mainChainAPIPort/api/Dashboard/AsyncLoopsStats | Select-Object -ExpandProperty content 
    if ( $AsyncLoopStats.Contains("Fault Reason: Missing outpoint data") )
    {
        Write-Host "ERROR: Indexing Database is corrupt" -ForegroundColor Red
        Write-Host "Would you like to delete the database?"
        $DeleteDB = Read-Host -Prompt "Enter 'Yes' to remove the Indexing Database or 'No' to exit the script"
        While ( $DeleteDB -ne "Yes" -and $DeleteDB -ne "No" )
        {
            $DeleteDB = Read-Host -Prompt "Enter 'Yes' to remove the indexing database or 'No' to exit the script"
        }
        Switch ( $DeleteDB )
        {
            Yes 
            {
                Shutdown-MainchainNode
                Remove-Item -Path $mainChainDataDir\addressindex.litedb -Force
                if ( -not ( Get-Item -Path $mainChainDataDir\addressindex.litedb ) )
                {
                    Write-Host "SUCCESS: Indexing Database has been removed. Please re-run the script" -ForegroundColor Green
                    Start-Sleep 10
                    Exit
                }
                    Else
                    {
                        Write-Host "ERROR: Something went wrong. Please contact support in Discord" -ForegroundColor Red
                    }
            }
                
            No 
            { 
                Shutdown-MainchainNode
                Write-Host "WARNING: Masternode cannot run until Indexing Database is recovered. This will require a re-index. Please remove the addressindex.litedb file and re-run the script" -ForegroundColor DarkYellow
                Start-Sleep 10
                Exit
            }
        }
    }
}

function Shutdown-MainchainNode
{
    Write-Host "Shutting down Mainchain Node..." -ForegroundColor Yellow
    $Headers = @{}
    $Headers.Add("Accept","application/json")
    Invoke-WebRequest -Uri http://localhost:$mainChainAPIPort/api/Node/shutdown -Method Post -ContentType application/json-patch+json -Headers $Headers -Body "true" -ErrorAction SilentlyContinue | Out-Null

    While ( Test-Connection -TargetName 127.0.0.1 -TCPPort $mainChainAPIPort -ErrorAction SilentlyContinue )
    {
        Write-Host "Waiting for node to stop..." -ForegroundColor Yellow
        Start-Sleep 5
    }

    Write-Host "SUCCESS: Mainchain Node shutdown" -ForegroundColor Green
    Write-Host ""
}

function Shutdown-SidechainNode
{
    Write-Host "Shutting down Sidechain Node..." -ForegroundColor Yellow
    $Headers = @{}
    $Headers.Add("Accept","application/json")
    Invoke-WebRequest -Uri http://localhost:$sideChainAPIPort/api/Node/shutdown -Method Post -ContentType application/json-patch+json -Headers $Headers -Body "true" -ErrorAction SilentlyContinue | Out-Null

    While ( Test-Connection -TargetName 127.0.0.1 -TCPPort $sideChainAPIPort -ErrorAction SilentlyContinue )
    {
        Write-Host "Waiting for node to stop..." -ForegroundColor Yellow
        Start-Sleep 5
    }

     Write-Host "SUCCESS: Sidechain Node shutdown" -ForegroundColor Green
     Write-Host ""
}

function Get-MaxHeight 
{
    $Height = @{}
    $Peers = Invoke-WebRequest -Uri http://localhost:$API/api/ConnectionManager/getpeerinfo | ConvertFrom-Json
    foreach ( $Peer in $Peers )
    {
        if ( $Peer.subver -eq "StratisNode:0.13.0 (70000)" )
        {
        }
            Else
            {
                $Height.Add($Peer.id,$Peer.startingheight)
            }
    }    
    
    $MaxHeight = $Height.Values | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum
    $MaxHeight
}       

function Get-LocalHeight 
{
    $StatsRequest = Invoke-WebRequest -Uri http://localhost:$API/api/Node/status
    $Stats = ConvertFrom-Json $StatsRequest
    $LocalHeight = $Stats.blockStoreHeight
    $LocalHeight
}

function Get-LocalIndexerHeight 
{
    $IndexStatsRequest = Invoke-WebRequest -Uri http://localhost:$API/api/BlockStore/addressindexertip
    $IndexStats = ConvertFrom-Json $IndexStatsRequest
    $LocalIndexHeight = $IndexStats.tipHeight
    $LocalIndexHeight
}

function Get-BlockStoreStatus
{
    $FeatureStatus = Invoke-WebRequest -Uri http://localhost:$API/api/Node/status | ConvertFrom-Json | Select-Object -ExpandProperty featuresData
    $BlockStoreStatus = $FeatureStatus | Where-Object { $_.namespace -eq "Stratis.Bitcoin.Features.BlockStore.BlockStoreFeature" }
    $BlockStoreStatus.state
}

function Shutdown-Dashboard
{
    Write-Host "Shutting down Stratis Masternode Dashboard..." -ForegroundColor Yellow
    Start-Job -ScriptBlock { Invoke-WebRequest -Uri http://localhost:37000/shutdown } | Out-Null
        While ( Test-Connection -TargetName 127.0.0.1 -TCPPort 37000 -ErrorAction SilentlyContinue )
        {
            Write-Host "Waiting for Stratis Masternode Dashboard to shut down" -ForegroundColor Yellow
            Start-Sleep 5
        }
}

function Check-TimeDifference
{
    Write-Host "Checking UTC Time Difference" -ForegroundColor Cyan
    $timeDif = New-TimeSpan -Start (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") -End ( Invoke-WebRequest http://worldtimeapi.org/api/timezone/UTC | ConvertFrom-Json | Select-Object -ExpandProperty utc_datetime ) | Select-Object -ExpandProperty TotalSeconds
    if ( $timeDif -gt 8 )
    {
        Write-Host "ERROR: System Time is not accurate. Currently $timeDif seconds diffence with actual time! Correct Time & Date and restart" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }
        Else
        {
            Write-Host "SUCCESS: Time difference is $timeDif seconds" -ForegroundColor Green
            Write-Host ""
        }
}

#Create DataDir(s)
if ( -not ( Get-Item -Path $mainChainDataDir -ErrorAction SilentlyContinue ) )
{
    New-Item -ItemType Directory -Path $mainChainDataDir
}

if ( -not ( Get-Item -Path $sideChainDataDir -ErrorAction SilentlyContinue ) )
{
    New-Item -ItemType Directory -Path $sideChainDataDir
}

#Gather Federation Detail
if ( -not ( Test-Path $sideChainDataDir\federationKey.dat ) ) 
{
    $miningDAT = Read-Host "Please Enter the full path to the federationKey.dat"
    Copy-Item $miningDAT -Destination $sideChainDataDir -Force -ErrorAction Stop
}

#Establish Node Type
if ( $multiSigMnemonic -ne $null )
{
    $NodeType = "50K"
}

#Check for Completion
if ( $NodeType -eq "50K" )
{
    if ( -not ( $multiSigPassword ) ) { $varError = 1 }
}
    Else
    {
        if ( -not ( $miningPassword ) ) { $varError = 1 }
    }

if ( -not ( $mainChainDataDir )  ) { $varError = 1 }
if ( -not ( $sideChainDataDir )  ) { $varError = 1 }
if ( -not ( Test-Path $sideChainDataDir/federationKey.dat ) ) { $varError = 1 }

if ( $varError -eq '1' )  
{
    Write-Host "ERROR: Some Values were not set. Please re-run this script" -ForegroundColor Red
    Start-Sleep 30
    Exit
}

#Clear Host
Clear-Host

#Check for an existing running node
Write-Host "Checking for running Mainchain Node" -ForegroundColor Cyan
if ( Test-Connection -TargetName 127.0.0.1 -TCPPort $mainChainAPIPort )
{
    Write-Host "WARNING: A node is already running, will perform a graceful shutdown" -ForegroundColor DarkYellow
    Write-Host ""
    Shutdown-MainchainNode
}

Write-Host "Checking for running Sidechain Node" -ForegroundColor Cyan
if ( Test-Connection -TargetName 127.0.0.1 -TCPPort $sideChainAPIPort )
{
    Write-Host "WARNING: A node is already running, will perform a graceful shutdown" -ForegroundColor DarkYellow
    Write-Host ""
    Shutdown-SidechainNode
}

#Check for running dashboard
Write-Host "Checking for the Stratis Masternode Dashboard" -ForegroundColor Cyan
if ( Test-Connection -TargetName 127.0.0.1 -TCPPort 37000 -ErrorAction SilentlyContinue )
{
    Write-Host "WARNING: The Stratis Masternode Dashboard is already running, will perform a graceful shutdown" -ForegroundColor DarkYellow
    Write-Host ""
    Shutdown-Dashboard
}

Write-Host ""

#Check Time Difference
Check-TimeDifference

if ( $NodeType -eq "50K" ) 
{
    #Move to CirrusPegD
    Set-Location -Path $cloneDir/src/Stratis.CirrusPegD
}
    Else
    {
        #Move to CirrusMinerD
        Set-Location -Path $cloneDir/src/Stratis.CirrusMinerD
    }

#Start Mainchain Node
$API = $mainChainAPIPort
Write-Host "Starting Mainchain Masternode" -ForegroundColor Cyan
if ( $NodeType -eq "50K" ) 
{
    $StartNode = Start-Process dotnet -ArgumentList "run -mainchain -addressindex=1 -apiport=$mainChainAPIPort -counterchainapiport=$sideChainAPIPort -redeemscript=""$redeemscript"" -publickey=$multiSigPublicKey -federationips=$federationIPs" -PassThru
}
    Else
    {
        $StartNode = Start-Process dotnet -ArgumentList "run -mainchain -addressindex=1 -apiport=$mainChainAPIPort" -PassThru
    }

#Wait for API
While ( -not ( Test-Connection -TargetName 127.0.0.1 -TCPPort $API ) ) 
{
    Write-Host "Waiting for API..." -ForegroundColor Yellow  
    Start-Sleep 3
    if ( $StartNode.HasExited -eq $true )
    {
        Write-Host "ERROR: Something went wrong. Please contact support in Discord" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }
}

#Wait for BlockStore Feature
While ( ( Get-BlockStoreStatus ) -ne "Initialized" )  
{ 
    Write-Host "Waiting for BlockStore to Initialize..." -ForegroundColor Yellow
    Start-Sleep 10
    if ( $StartNode.HasExited -eq $true )
    {
        Write-Host "ERROR: Something went wrong. Please contact support in Discord" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }
}

#Wait for IBD
While ( ( Get-MaxHeight ) -eq $null ) 
{
    Write-Host "Waiting for Peers..." -ForegroundColor Yellow
    Start-Sleep 10
}

While ( ( Get-MaxHeight ) -gt ( Get-LocalIndexerHeight ) ) 
{
    $a = Get-MaxHeight
    $b = Get-LocalIndexerHeight 
    $c = $a - $b
    Write-Host ""
    Write-Host "The Indexed Height is $b" -ForegroundColor Yellow
    Write-Host "The Current Tip is $a" -ForegroundColor Yellow
    Write-Host "$c Blocks Require Indexing..." -ForegroundColor Yellow
    Start-Sleep 10
    Get-IndexerStatus
}

#Clear Variables
if ( Get-Variable a -ErrorAction SilentlyContinue ) { Clear-Variable a }
if ( Get-Variable b -ErrorAction SilentlyContinue ) { Clear-Variable b }
if ( Get-Variable c -ErrorAction SilentlyContinue ) { Clear-Variable c }

#Start Sidechain Node
$API = $sideChainAPIPort
Write-Host "Starting Sidechain Masternode" -ForegroundColor Cyan
if ( $NodeType -eq "50K" ) 
{
    $StartNode = Start-Process dotnet -ArgumentList "run -sidechain -apiport=$sideChainAPIPort -counterchainapiport=$mainChainAPIPort -redeemscript=""$redeemscript"" -publickey=$multiSigPublicKey -federationips=$federationIPs" -PassThru
}
    Else
    {
        $StartNode = Start-Process dotnet -ArgumentList "run -sidechain -apiport=$sideChainAPIPort -counterchainapiport=$mainChainAPIPort" -PassThru
    }

#Wait for API
While ( -not ( Test-Connection -TargetName 127.0.0.1 -TCPPort $API ) ) 
{
    Write-Host "Waiting for API..." -ForegroundColor Yellow  
    Start-Sleep 3
    if ( $StartNode.HasExited -eq $true )
    {
        Write-Host "ERROR: Something went wrong. Please contact support in Discord" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }
}

#Wait for BlockStore Feature
While ( ( Get-BlockStoreStatus ) -ne "Initialized" )  
{ 
    Write-Host "Waiting for BlockStore to Initialize..." -ForegroundColor Yellow
    Start-Sleep 10
    if ( $StartNode.HasExited -eq $true )
    {
        Write-Host "ERROR: Something went wrong. Please contact support in Discord" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }
}

#Wait for IBD
While ( ( Get-MaxHeight ) -eq $null ) 
{
Write-Host "Waiting for Peers..." -ForegroundColor Yellow
Start-Sleep 10
}

While ( ( Get-MaxHeight ) -gt ( Get-LocalHeight ) ) 
{
    $a = Get-MaxHeight
    $b = Get-LocalHeight 
    $c = $a - $b
    Write-Host ""
    Write-Host "The Local Synced Height is $b" -ForegroundColor Yellow
    Write-Host "The Current Tip is $a" -ForegroundColor Yellow
    Write-Host "$c Blocks are Required..." -ForegroundColor Yellow
    Start-Sleep 10
}

#Mining Wallet Creation
if ( -not ( Test-Path -Path $sideChainDataDir\MiningWallet.wallet.json ) ) 
{
    Write-Host "Creating Mining Wallet" -ForegroundColor Cyan
    $Body = @{} 
    $Body.Add("name","MiningWallet")

    if ( $NodeType -eq "50K" )
    {    
        $Body.Add("password",$multiSigPassword)
        $Body.Add("passphrase",$multiSigPassword)
        $Body.Add("mnemonic",$multiSigMnemonic)
    }
        Else
        {
            $Body.Add("password",$miningPassword)
            $Body.Add("passphrase",$miningPassword)
        }
    
    $Body = $Body | ConvertTo-Json
        
    Invoke-WebRequest -Uri http://localhost:$sideChainAPIPort/api/Wallet/create -Method Post -Body $Body -ContentType "application/json" | Out-Null
    
    $Body = @{}
    $Body.Add("name","MiningWallet")
        
    if ( $NodeType -eq "50K" )
    {    
        $Body.Add("password",$multiSigPassword)
    }
        Else
        {
            $Body.Add("password",$miningPassword)
        }
                   
    $Body = $Body | ConvertTo-Json
           
    Invoke-WebRequest -Uri http://localhost:$sideChainAPIPort/api/Wallet/load -Method Post -Body $Body -ContentType "application/json" | Out-Null
}

if ( $NodeType -eq "50K" )
{
    #Enable Federation
    Write-Host "Enabling Federation" -ForegroundColor Cyan

    $Body = @{} 
    $Body.Add("password",$multiSigPassword)
    $Body.Add("passphrase",$multiSigPassword)
    $Body.Add("mnemonic",$multiSigMnemonic)
    $Body = $Body | ConvertTo-Json

    Invoke-WebRequest -Uri http://localhost:$sideChainAPIPort/api/FederationWallet/enable-federation -Method Post -Body $Body -ContentType "application/json" | Out-Null
    Write-Host "Sidechain Gateway Enabled" -ForegroundColor Cyan

    Invoke-WebRequest -Uri http://localhost:$mainChainAPIPort/api/FederationWallet/enable-federation -Method Post -Body $Body -ContentType "application/json" | Out-Null
    Write-Host "Mainchain Gateway Enabled" -ForegroundColor Cyan

    #Checking Mainchain Federation Status
    $MainchainFedInfo = Invoke-WebRequest -Uri http://localhost:$mainChainAPIPort/api/FederationGateway/info | Select-Object -ExpandProperty Content | ConvertFrom-Json
    if ( $MainchainFedInfo.active -ne "True" )
    {
        Write-Host "ERROR: Something went wrong. Federation Inactive! Please contact support in Discord" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }

    #Checking Sidechain Federation Status
    $SidechainFedInfo = Invoke-WebRequest -Uri http://localhost:$sideChainAPIPort/api/FederationGateway/info | Select-Object -ExpandProperty Content | ConvertFrom-Json
    if ( $SidechainFedInfo.active -ne "True" )
    {
        Write-Host "ERROR: Federation Inactive, ensure correct mnemonic words were entered. Please contact support in Discord" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }
}

#Checking Node Ports
if ( ( Test-Connection -TargetName 127.0.0.1 -TCPPort $mainChainAPIPort ) -and ( Test-Connection -TargetName 127.0.0.1 -TCPPort $sideChainAPIPort ) )
{
    Write-Host "SUCCESS: Masternode is running" -ForegroundColor Green
    Start-Sleep 10
}
    Else
    {
        Write-Host "ERROR: Cannot connect to nodes! Please contact support in Discord" -ForegroundColor Red
        Start-Sleep 30
        Exit
    }

Write-Host ""

#Launching Masternode Dashboard
Set-Location $stratisMasternodeDashboardCloneDir
if ( $NodeType -eq "50K" )
{
    Write-Host "Starting Stratis Masternode Dashboard (50K Mode)" -ForegroundColor Cyan
    $Clean = Start-Process dotnet.exe -ArgumentList "clean" -PassThru
    While ( $Clean.HasExited -ne $true )  
    {
        Write-Host "Cleaning Stratis Masternode Dashboard..." -ForegroundColor Yellow
        Start-Sleep 3
    }
    Start-Process dotnet.exe -ArgumentList "run --nodetype 50K --mainchainport $mainChainAPIPort --sidechainport $sideChainAPIPort --env mainnet" -WindowStyle Hidden
}
    Else
    {
        Write-Host "Starting Stratis Masternode Dashboard (10K Mode)" -ForegroundColor Cyan
        $Clean = Start-Process dotnet.exe -ArgumentList "clean" -PassThru
        While ( $Clean.HasExited -ne $true ) 
        {
            Write-Host "Cleaning Stratis Masternode Dashboard..." -ForegroundColor Yellow
            Start-Sleep 3
        }
        Start-Process dotnet.exe -ArgumentList "run --nodetype 10K --mainchainport $mainChainAPIPort --sidechainport $sideChainAPIPort --env mainnet" -WindowStyle Hidden
    }

While ( -not ( Test-Connection -TargetName 127.0.0.1 -TCPPort 37000 -ErrorAction SilentlyContinue ) )
{
    Write-Host "Waiting for Stratis Masternode Dashboard..." -ForegroundColor Yellow
    Start-Sleep 3
}

Start-Process http://localhost:37000
Write-Host "SUCCESS: Stratis Masternode Dashboard launched" -ForegroundColor Green

Exit
