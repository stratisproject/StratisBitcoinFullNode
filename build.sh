#!/bin/bash
dotnet --info
echo STARTED dotnet restore
dotnet restore -v m
echo STARTED dotnet build
dotnet build -c Release ${path} -v m
echo STARTED dotnet test
dotnet test -c Release ./Stratis.Bitcoin.Tests/Stratis.Bitcoin.Tests.csproj -v m
