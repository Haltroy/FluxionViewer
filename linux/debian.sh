#!/usr/bin/env bash

publishDir="${1}"
arch="${2}"
deb_arch="${arch}"
SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)

echo Cleanup
rm -R ${SCRIPT_DIR}/${arch}/

if [ "$arch" = "x64" ]; then
    FV_ARCH="amd64"
elif [ "$arch" = "arm64" ]; then
    FV_ARCH="aarch64"
fi

echo Write Debian control
mkdir -p ${SCRIPT_DIR}/${arch}/DEBIAN
cat >${SCRIPT_DIR}/${arch}/DEBIAN/control <<EOL
Package: fluxionviewer
Version: 1.1
Section: devel
Priority: optional
Architecture: ${deb_arch}
Depends: libx11-6, libice6, libsm6, libfontconfig1, ca-certificates, tzdata, libc6, libgcc1 | libgcc-s1, libgssapi-krb5-2, libstdc++6, zlib1g, libssl1.0.0 | libssl1.0.2 | libssl1.1 | libssl3, libicu | libicu74 | libicu72 | libicu71 | libicu70 | libicu69 | libicu68 | libicu67 | libicu66 | libicu65 | libicu63 | libicu60 | libicu57 | libicu55 | libicu52
Maintainer: haltroy <thehaltroy@gmail.com>
Homepage: https://github.com/haltroy/FluxionViewer
Description: Edit and view Fluxion files.
Copyright: 2022-2024 haltroy <thehaltroy@gmail.com>
EOL

echo Copy shortcut script
mkdir -p ${SCRIPT_DIR}/${arch}/usr/bin
cp ${SCRIPT_DIR}/shortcut ${SCRIPT_DIR}/${arch}/usr/bin/fluxionviewer
chmod +x ${SCRIPT_DIR}/${arch}/usr/bin/fluxionviewer

echo Copy desktop entry
mkdir -p ${SCRIPT_DIR}/${arch}/usr/share/applications
cp ${SCRIPT_DIR}/desktop.desktop ${SCRIPT_DIR}/${arch}/usr/share/applications/fluxionviewer.desktop

echo Copy icon
mkdir -p ${SCRIPT_DIR}/${arch}/usr/share/icons/hicolor/2048x2048/apps
cp ${SCRIPT_DIR}/../src/FluxionViewer/Assets/logo.png ${SCRIPT_DIR}/${arch}/usr/share/icons/hicolor/2048x2048/apps/fluxionviewer.png

echo Copy executable
mkdir -p ${SCRIPT_DIR}/${arch}/usr/lib/fluxionviewer
cp -r ${publishDir}/. ${SCRIPT_DIR}/${arch}/usr/lib/fluxionviewer/

echo Make the package
dpkg-deb --root-owner-group --build ${SCRIPT_DIR}/${arch}/ ${SCRIPT_DIR}/fluxionviewer.${deb_arch}.deb

echo Cleanup
rm -R ${SCRIPT_DIR}/${arch}/
