#!/bin/bash
dotnet --info
echo STARTED dotnet build
cd src
dotnet build -c Release ${path} -v m

echo STARTED dotnet test (unit tests)

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

echo FINISHED dotnet unit tests
if [[ $ANYFAILURES == "true" ]] ; then
	exit 1
fi

echo STARTED dotnet test (integration tests)
ANYFAILURES=false
for integrationTestProject in *.IntegrationTests; do

	echo "Processing $testProject file.."; 
	cd $integrationTestProject
	COMMAND="dotnet test --no-build -c Release -v m"
	$COMMAND
	EXITCODE=$?
	echo exit code for $integrationTestProject: $EXITCODE

	if [ $EXITCODE -ne 0 ] ; then
		ANYFAILURES=true
	fi

	cd ..
done