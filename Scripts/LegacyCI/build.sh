#!/bin/bash
dotnet --info

echo disabling Fody because it is not needed for CI

cd src
dotnet remove Stratis.Bitcoin/Stratis.Bitcoin.csproj package Tracer.Fody

echo STARTED dotnet build
dotnet build -c Release ${path} -v m

echo STARTED dotnet test

ANYFAILURES=false
for testProject in *.Tests; do

# exclude integration tests
if [[ "$testProject" == *"Integration.Tests"* ]] || [[ "$testProject" == *"IntegrationTests"* ]] ; then
    continue
fi

echo "Processing $testProject file.."; 
cd $testProject
COMMAND="dotnet test --no-build -c Release -v m"
$COMMAND
EXITCODE=$?
echo exit code for $testProject: $EXITCODE

if [ $EXITCODE -ne 0 ] ; then
    ANYFAILURES=true
fi

cd ..
done

echo FINISHED dotnet test
if [[ $ANYFAILURES == "true" ]] ; then
    exit 1
fi
