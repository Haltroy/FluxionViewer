#!/usr/bin/env bash

publishDir="${1}"
SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)

echo Cleanup
rm -R ${SCRIPT_DIR}/arm64/

echo Copy Debian control
mkdir -p ${SCRIPT_DIR}/arm64/DEBIAN
cp ${SCRIPT_DIR}/control-arm64 ${SCRIPT_DIR}/arm64/DEBIAN/control

echo Copy shortcut script
mkdir -p ${SCRIPT_DIR}/arm64/usr/bin
cp ${SCRIPT_DIR}/../shortcut ${SCRIPT_DIR}/arm64/usr/bin/fluxionviewer
chmod +x ${SCRIPT_DIR}/arm64/usr/bin/fluxionviewer

echo Copy desktop entry
mkdir -p ${SCRIPT_DIR}/arm64/usr/share/applications
cp ${SCRIPT_DIR}/../desktop.desktop ${SCRIPT_DIR}/arm64/usr/share/applications/fluxionviewer.desktop

echo Copy icon
mkdir -p ${SCRIPT_DIR}/arm64/usr/share/icons/hicolor/2048x2048/apps
cp ${SCRIPT_DIR}/../../src/FluxionViewer/Assets/logo.png ${SCRIPT_DIR}/arm64/usr/share/icons/hicolor/2048x2048/apps/fluxionviewer.png

echo Copy executable
mkdir -p ${SCRIPT_DIR}/arm64/usr/lib/fluxionviewer
cp -r ${publishDir}/. ${SCRIPT_DIR}/arm64/usr/lib/fluxionviewer/

echo Make the package
dpkg-deb --root-owner-group --build ${SCRIPT_DIR}/arm64/ ${publishDir}/../fluxionviewer.aarch64.deb
