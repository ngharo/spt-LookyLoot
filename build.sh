#!/bin/bash
PLUGIN="bin/Release/netstandard2.1/LookyLoot.dll"
VERSION="v$(grep '<Version>' LookyLoot.csproj | grep -Eo '[0-9.]+')"
ZIPFILE="LookyLoot-${VERSION}.zip"

echo "Building LookyLoot ${VERSION}"

mise install

if [[ "$1" == "install" ]]; then
  mise exec -- dotnet build -c Release -p:InstallPlugin=true
  exit 0
fi

rm -f "$ZIPFILE"
mise exec -- dotnet build -c Release
mkdir -p dist/BepInEx/plugin/LookyLoot

cp -v "$PLUGIN" dist/BepInEx/plugin/LookyLoot/
(cd dist && zip -r "../${ZIPFILE}" BepInEx)

rm -rf dist
