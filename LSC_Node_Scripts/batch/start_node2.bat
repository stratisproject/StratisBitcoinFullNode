@ECHO OFF 
set Message=Changing dir...
set NodesDirectory=%APPDATA%\LocalSCNodes
@ECHO ON
echo %Message%
@ECHO OFF 
cd ..\..
git checkout LSC-tutorial
cd src\Stratis.LocalSmartContracts.NodeD
@ECHO ON
echo Running standard node 2...
echo **Data held in %NodesDirectory%\node2**
dotnet run -datadir=%NodesDirectory%\node2 -port=36203 -apiport=38203 -addnode=127.0.0.1:36201 -bind=127.0.0.1