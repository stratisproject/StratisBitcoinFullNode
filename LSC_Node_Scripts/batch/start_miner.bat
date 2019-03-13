@ECHO OFF 
set Message=Changing dir...
set NodesDirectory=%APPDATA%\LocalSCNodes
@ECHO ON
echo %Message%
@ECHO OFF 
cd ..\..
git checkout LSC-tutorial
cd src\Stratis.LocalSmartContracts.MinerD
@ECHO ON
echo Running miner 1...
echo **Data held in %NodesDirectory%\miner1**
dotnet run -bootstrap -datadir=%NodesDirectory%\miner1 -port=36201 -apiport=38201 -txindex=1 connect=0 listen=1 -bind=127.0.0.1