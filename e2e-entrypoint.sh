#!/bin/sh

echo "Starting ReadyStackGo..."

# Just start the application
exec dotnet ReadyStackGo.Api.dll
