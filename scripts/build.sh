#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."
dotnet test tests/JellyDaily.Tests/JellyDaily.Tests.csproj -c Release
dotnet publish src/JellyDaily/JellyDaily.csproj -c Release --no-restore -o artifacts/plugin
# Jellyfin provides its own API assemblies; install only the plugin DLL.
