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

function Get-Median($numberSeries)
{
    $sortedNumbers = @($numberSeries | Sort-Object)
    if ( $numberSeries.Count % 2 ) 
	{
	    # Odd, pick the middle
        $sortedNumbers[(($sortedNumbers.Count - 1) / 2)]
    } 
		Else 
		{
			# Even, average the middle two
			($sortedNumbers[($sortedNumbers.Count / 2)] + $sortedNumbers[($sortedNumbers.Count / 2) - 1]) / 2
		}
}

function Check-TimeDifference
{
    Write-Host "Checking UTC Time Difference" -ForegroundColor Cyan
    $timeDifSamples = @([int16]::MaxValue,[int16]::MaxValue,[int16]::MaxValue)
    $timeDifSamples[0] = New-TimeSpan -Start (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") -End ( Invoke-WebRequest http://worldtimeapi.org/api/timezone/UTC | ConvertFrom-Json | Select-Object -ExpandProperty utc_datetime ) | Select-Object -ExpandProperty TotalSeconds
    $timeDifSamples[1] = New-TimeSpan -Start (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") -End ( Invoke-WebRequest http://worldtimeapi.org/api/timezone/UTC | ConvertFrom-Json | Select-Object -ExpandProperty utc_datetime ) | Select-Object -ExpandProperty TotalSeconds
    $timeDifSamples[2] = New-TimeSpan -Start (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") -End ( Invoke-WebRequest http://worldtimeapi.org/api/timezone/UTC | ConvertFrom-Json | Select-Object -ExpandProperty utc_datetime ) | Select-Object -ExpandProperty TotalSeconds

    $timeDif = Get-Median -numberSeries $timeDifSamples

    if ( $timeDif -gt 2 )
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
$varError = $false

if ( $NodeType -eq "50K" )
{
    if ( -not ( $multiSigPassword ) ) 
	{ 
		$varError = $true 
	}
}
	ElseIf ( -not ( $miningPassword ) ) 
	{ 
		$varError = $true 
	}

if ( -not ( $mainChainDataDir )  ) { $varError = $true }
if ( -not ( $sideChainDataDir )  ) { $varError = $true }
if ( -not ( Test-Path $sideChainDataDir/federationKey.dat ) ) { $varError = $true }
if ( $varError -eq $true )  
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
    $StartNode = Start-Process dotnet -ArgumentList "run -c Release -- -mainchain -addressindex=1 -apiport=$mainChainAPIPort -counterchainapiport=$sideChainAPIPort -redeemscript=""$redeemscript"" -publickey=$multiSigPublicKey -federationips=$federationIPs" -PassThru
}
    Else
    {
        $StartNode = Start-Process dotnet -ArgumentList "run -c Release -- -mainchain -addressindex=1 -apiport=$mainChainAPIPort" -PassThru
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
    $StartNode = Start-Process dotnet -ArgumentList "run -c Release -- -sidechain -apiport=$sideChainAPIPort -counterchainapiport=$mainChainAPIPort -redeemscript=""$redeemscript"" -publickey=$multiSigPublicKey -federationips=$federationIPs" -PassThru
}
    Else
    {
        $StartNode = Start-Process dotnet -ArgumentList "run -c Release -- -sidechain -apiport=$sideChainAPIPort -counterchainapiport=$mainChainAPIPort" -PassThru
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
    Start-Process dotnet.exe -ArgumentList "run -c Release -- --nodetype 50K --mainchainport $mainChainAPIPort --sidechainport $sideChainAPIPort --env mainnet" -WindowStyle Hidden
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
        Start-Process dotnet.exe -ArgumentList "run -c Release --nodetype 10K --mainchainport $mainChainAPIPort --sidechainport $sideChainAPIPort --env mainnet" -WindowStyle Hidden
    }

While ( -not ( Test-Connection -TargetName 127.0.0.1 -TCPPort 37000 -ErrorAction SilentlyContinue ) )
{
    Write-Host "Waiting for Stratis Masternode Dashboard..." -ForegroundColor Yellow
    Start-Sleep 3
}

Start-Process http://localhost:37000
Write-Host "SUCCESS: Stratis Masternode Dashboard launched" -ForegroundColor Green

Exit

# SIG # Begin signature block
# MIIO+wYJKoZIhvcNAQcCoIIO7DCCDugCAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUN4vIZBKJO8jN3jgRZgFRBbSN
# psugggxDMIIFfzCCBGegAwIBAgIQB+RAO8y2U5CYymWFgvSvNDANBgkqhkiG9w0B
# AQsFADBsMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYD
# VQQLExB3d3cuZGlnaWNlcnQuY29tMSswKQYDVQQDEyJEaWdpQ2VydCBFViBDb2Rl
# IFNpZ25pbmcgQ0EgKFNIQTIpMB4XDTE4MDcxNzAwMDAwMFoXDTIxMDcyMTEyMDAw
# MFowgZ0xEzARBgsrBgEEAYI3PAIBAxMCR0IxHTAbBgNVBA8MFFByaXZhdGUgT3Jn
# YW5pemF0aW9uMREwDwYDVQQFEwgxMDU1MDMzMzELMAkGA1UEBhMCR0IxDzANBgNV
# BAcTBkxvbmRvbjEaMBgGA1UEChMRU3RyYXRpcyBHcm91cCBMdGQxGjAYBgNVBAMT
# EVN0cmF0aXMgR3JvdXAgTHRkMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKC
# AQEAszr/7HowdxN95x+Utcge+d7wKwUA+kaIKrmlLFqFVg8ZdyvOZQN6gU/RRy6F
# NceRr5YAek4cg2T2MQs7REdkHDFzkAhOb1m/9b6fOJoF6YG3owhlyQZmtD0H64sj
# ZEpLkiuOFDOjxk8ICPrsoHcki5qoKdy7WkKdCWCTuSHKLNPUpfGyHYhjcdB5DTO2
# S4P+9JbIidPn0LR/NpjVCQXzFTTtteT+qj2cRTC4+ITIGWaWhulNiemLMSCF7Aar
# SAOQU8TSM9hLs4lwhtaLfx9j8bnQzz0YSYHUforeYrQCxmD66/KAup/OAZYQ6rYU
# kv5Eq+aNLd2Ot1FC1mQ3hOJW0QIDAQABo4IB6TCCAeUwHwYDVR0jBBgwFoAUj+h+
# 8G0yagAFI8dwl2o6kP9r6tQwHQYDVR0OBBYEFIV31zNBlgBtHnHANerRL6iOxCqa
# MCYGA1UdEQQfMB2gGwYIKwYBBQUHCAOgDzANDAtHQi0xMDU1MDMzMzAOBgNVHQ8B
# Af8EBAMCB4AwEwYDVR0lBAwwCgYIKwYBBQUHAwMwewYDVR0fBHQwcjA3oDWgM4Yx
# aHR0cDovL2NybDMuZGlnaWNlcnQuY29tL0VWQ29kZVNpZ25pbmdTSEEyLWcxLmNy
# bDA3oDWgM4YxaHR0cDovL2NybDQuZGlnaWNlcnQuY29tL0VWQ29kZVNpZ25pbmdT
# SEEyLWcxLmNybDBLBgNVHSAERDBCMDcGCWCGSAGG/WwDAjAqMCgGCCsGAQUFBwIB
# FhxodHRwczovL3d3dy5kaWdpY2VydC5jb20vQ1BTMAcGBWeBDAEDMH4GCCsGAQUF
# BwEBBHIwcDAkBggrBgEFBQcwAYYYaHR0cDovL29jc3AuZGlnaWNlcnQuY29tMEgG
# CCsGAQUFBzAChjxodHRwOi8vY2FjZXJ0cy5kaWdpY2VydC5jb20vRGlnaUNlcnRF
# VkNvZGVTaWduaW5nQ0EtU0hBMi5jcnQwDAYDVR0TAQH/BAIwADANBgkqhkiG9w0B
# AQsFAAOCAQEAadI2wIeKuOS2d3N49ilrMWEESf91zG6ifoJAES78+Q1X3pGWFltH
# h3J66FOwtC5XYg+UP3MDQybGb+yNBnABypIRE8RJhcmPeRbHjTqA2txl3B16evUm
# JX4Esmc7NOraGn03S9ZMH8Fa2coX/Epb/RbvY4e/z0O5dOsknfBOKXCEKrjzGVxt
# p9WIksRQLRdL0zqkKsxAU8gyU6O0neOCO4sYXvAb2CuLxJNMkEUO8mZe1Sz0DRLa
# hHueLB2EoKlyhFvA8SjehIcLQlE5FQvvxqmyy1yBovAWL6ktCaFCN6bLe/WTWPtu
# g5NNcn4cvq7X5gXQ8iNAPw+ZHmBipK0GXDCCBrwwggWkoAMCAQICEAPxtOFfOoLx
# FJZ4s9fYR1wwDQYJKoZIhvcNAQELBQAwbDELMAkGA1UEBhMCVVMxFTATBgNVBAoT
# DERpZ2lDZXJ0IEluYzEZMBcGA1UECxMQd3d3LmRpZ2ljZXJ0LmNvbTErMCkGA1UE
# AxMiRGlnaUNlcnQgSGlnaCBBc3N1cmFuY2UgRVYgUm9vdCBDQTAeFw0xMjA0MTgx
# MjAwMDBaFw0yNzA0MTgxMjAwMDBaMGwxCzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxE
# aWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5jb20xKzApBgNVBAMT
# IkRpZ2lDZXJ0IEVWIENvZGUgU2lnbmluZyBDQSAoU0hBMikwggEiMA0GCSqGSIb3
# DQEBAQUAA4IBDwAwggEKAoIBAQCnU/oPsrUT8WTPhID8roA10bbXx6MsrBosrPGE
# rDo1EjqSkbpX5MTJ8y+oSDy31m7clyK6UXlhr0MvDbebtEkxrkRYPqShlqeHTyN+
# w2xlJJBVPqHKI3zFQunEemJFm33eY3TLnmMl+ISamq1FT659H8gTy3WbyeHhivgL
# DJj0yj7QRap6HqVYkzY0visuKzFYZrQyEJ+d8FKh7+g+03byQFrc+mo9G0utdrCM
# XO42uoPqMKhM3vELKlhBiK4AiasD0RaCICJ2615UOBJi4dJwJNvtH3DSZAmALeK2
# nc4f8rsh82zb2LMZe4pQn+/sNgpcmrdK0wigOXn93b89OgklAgMBAAGjggNYMIID
# VDASBgNVHRMBAf8ECDAGAQH/AgEAMA4GA1UdDwEB/wQEAwIBhjATBgNVHSUEDDAK
# BggrBgEFBQcDAzB/BggrBgEFBQcBAQRzMHEwJAYIKwYBBQUHMAGGGGh0dHA6Ly9v
# Y3NwLmRpZ2ljZXJ0LmNvbTBJBggrBgEFBQcwAoY9aHR0cDovL2NhY2VydHMuZGln
# aWNlcnQuY29tL0RpZ2lDZXJ0SGlnaEFzc3VyYW5jZUVWUm9vdENBLmNydDCBjwYD
# VR0fBIGHMIGEMECgPqA8hjpodHRwOi8vY3JsMy5kaWdpY2VydC5jb20vRGlnaUNl
# cnRIaWdoQXNzdXJhbmNlRVZSb290Q0EuY3JsMECgPqA8hjpodHRwOi8vY3JsNC5k
# aWdpY2VydC5jb20vRGlnaUNlcnRIaWdoQXNzdXJhbmNlRVZSb290Q0EuY3JsMIIB
# xAYDVR0gBIIBuzCCAbcwggGzBglghkgBhv1sAwIwggGkMDoGCCsGAQUFBwIBFi5o
# dHRwOi8vd3d3LmRpZ2ljZXJ0LmNvbS9zc2wtY3BzLXJlcG9zaXRvcnkuaHRtMIIB
# ZAYIKwYBBQUHAgIwggFWHoIBUgBBAG4AeQAgAHUAcwBlACAAbwBmACAAdABoAGkA
# cwAgAEMAZQByAHQAaQBmAGkAYwBhAHQAZQAgAGMAbwBuAHMAdABpAHQAdQB0AGUA
# cwAgAGEAYwBjAGUAcAB0AGEAbgBjAGUAIABvAGYAIAB0AGgAZQAgAEQAaQBnAGkA
# QwBlAHIAdAAgAEMAUAAvAEMAUABTACAAYQBuAGQAIAB0AGgAZQAgAFIAZQBsAHkA
# aQBuAGcAIABQAGEAcgB0AHkAIABBAGcAcgBlAGUAbQBlAG4AdAAgAHcAaABpAGMA
# aAAgAGwAaQBtAGkAdAAgAGwAaQBhAGIAaQBsAGkAdAB5ACAAYQBuAGQAIABhAHIA
# ZQAgAGkAbgBjAG8AcgBwAG8AcgBhAHQAZQBkACAAaABlAHIAZQBpAG4AIABiAHkA
# IAByAGUAZgBlAHIAZQBuAGMAZQAuMB0GA1UdDgQWBBSP6H7wbTJqAAUjx3CXajqQ
# /2vq1DAfBgNVHSMEGDAWgBSxPsNpA/i/RwHUmCYaCALvY2QrwzANBgkqhkiG9w0B
# AQsFAAOCAQEAGTNKDIEzN9utNsnkyTq7tRsueqLi9ENCF56/TqFN4bHb6YHdnwHy
# 5IjV6f4J/SHB7F2A0vDWwUPC/ncr2/nXkTPObNWyGTvmLtbJk0+IQI7N4fV+8Q/G
# WVZy6OtqQb0c1UbVfEnKZjgVwb/gkXB3h9zJjTHJDCmiM+2N4ofNiY0/G//V4BqX
# i3zabfuoxrI6Zmt7AbPN2KY07BIBq5VYpcRTV6hg5ucCEqC5I2SiTbt8gSVkIb7P
# 7kIYQ5e7pTcGr03/JqVNYUvsRkG4Zc64eZ4IlguBjIo7j8eZjKMqbphtXmHGlreK
# uWEtk7jrDgRD1/X+pvBi1JlqpcHB8GSUgDGCAiIwggIeAgEBMIGAMGwxCzAJBgNV
# BAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdp
# Y2VydC5jb20xKzApBgNVBAMTIkRpZ2lDZXJ0IEVWIENvZGUgU2lnbmluZyBDQSAo
# U0hBMikCEAfkQDvMtlOQmMplhYL0rzQwCQYFKw4DAhoFAKB4MBgGCisGAQQBgjcC
# AQwxCjAIoAKAAKECgAAwGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYB
# BAGCNwIBCzEOMAwGCisGAQQBgjcCARUwIwYJKoZIhvcNAQkEMRYEFPQv4HR+QpBr
# u62eVyExVL22rDNuMA0GCSqGSIb3DQEBAQUABIIBAAtdFjD2B/wGg+cBEpF+D0pD
# 9Rbl96Xdk4Qad6IqWid/hFRt9yTKLhScMZoTIwr871SEBjqeFyW/eog+R8FnLVbJ
# Dpaw6HKjWn5SZ5wv8wzLOBz9LI8qAodbjhNKtpsprxhM2ihHoz+2gId70IRYX6oG
# 8DMdCKdjFdpBN3DMZ9waP9x1OHB2sc7hQnA4Vapr1ahw2EGu4IK+T4X/gZxxOyNZ
# /iOep40N7O4gWYmOe2Ry3/3VYSsh7sXvvMmS1b2gjAMOlXwOIBdS1n6EIPTaoyXn
# chaMWPTlo8ATaTmbOqdEBD7A1IYaxDCEAjb3QMqfFTA7W6ziLFaeKZrSCHgvVmk=
# SIG # End signature block
